using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.VisualBasic;
using Microsoft.Win32.SafeHandles;
using ProtoBuf;
using SyncSharp.Common;
using SyncSharp.Common.model;
using Timer = System.Timers.Timer;

namespace SyncSharpWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        private NamedPipeServerStream _pipeServer;

        private Config _config;

        private readonly object _waitIntervalLock = new object();

        private readonly object _configChangedLock = new object();

        private bool _syncing;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("start");

            _pipeServer = new NamedPipeServerStream("syncsharp", PipeDirection.In,
                1);
            
            _config = GetConfig();

            return base.StartAsync(cancellationToken);
        }

  
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            Dispose();

            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //Wake main thread if cancelled
            stoppingToken.Register(() =>
            {
                lock (_waitIntervalLock)
                {
                    Monitor.Pulse(_waitIntervalLock);
                }
            });

            //Start server thread
            _ = Task.Run((async () =>
              {
                  //Continuously listen for new configs from the GUI 
                  outer: while (!stoppingToken.IsCancellationRequested)
                  {
                      _logger.LogDebug("Waiting on connection...");
                      await _pipeServer.WaitForConnectionAsync(stoppingToken);

                      const int bufferSize = 4000;
                      Memory<byte> buffer = new byte[bufferSize];
                      //Use list as a dynamic 'buffer'
                      var accList = new List<byte>();

                      _logger.LogDebug("Pipe Connected");

                      while (!stoppingToken.IsCancellationRequested)
                      {
                          //Reset Transfer size
                          var lastByteTransferSize = 0;

                          //Keep reading from stream until empty
                          do
                          {
                              var task = _pipeServer.ReadAsync(buffer, stoppingToken);
                              await task;
                              _logger.LogDebug($"Finished reading {task.Result} bytes from pipe");
                              lastByteTransferSize = task.Result;

                              //Move buffer to list and clear buffer
                              accList.AddRange(buffer.ToArray());
                              buffer.Span.Clear();
                          } while (lastByteTransferSize == bufferSize);

                          var bytesRead = accList.ToArray().AsSpan().TrimEnd((byte) 0).Length;

                          _logger.LogDebug($"finished reading a total of {bytesRead} bytes from pipe.");

                          //A one byte message means we should disconnect 
                          if (bytesRead == 1)
                          {
                              _logger.LogDebug("Disconnect bit received, disconnecting");
                              _pipeServer.Disconnect();
                              goto outer;
                          }

                          //Trim excess 0s from the end of buffer> WILL NOT serialize without this
                          var newConfig = Serializer.Deserialize<Config>(accList.ToArray().AsSpan().TrimEnd((byte) 0));

                          //Safely apply the new config
                          SetNewConfig(newConfig);

                          //Reset buffers
                          buffer.Span.Clear();
                          accList.Clear();
                      }
                  }
              }), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                //Wait time between syncs
                lock (_waitIntervalLock)
                {
                    _logger.LogDebug($"entering sleep for {_config.CheckInterval}");
                    Monitor.Wait(_waitIntervalLock, _config.CheckInterval);
                }

                _syncing = true;
                _logger.LogDebug($"woke, starting sync");
                await FileSyncUtility.Sync(_config,stoppingToken,_logger);
                _syncing = false;

                //Signal that syncs are done
                lock (_configChangedLock)
                {
                    Monitor.Pulse(_configChangedLock);
                }
            }
        }

        /// <summary>
        /// Sets the new config safely, waiting until syncs have completed.
        /// </summary>
        private void SetNewConfig(Config newConfig)
        {
            _logger.LogDebug(
                "new config received");

            if (_syncing)
            {
                //Wait until sync completes before setting new Config 
                lock (_configChangedLock)
                {
                    Monitor.Wait(_configChangedLock);
                }
            }

            //Migrate in memory sync times to the new config so we don't lose them 
            foreach (var path in newConfig.Paths)
            {
                var match = _config.Paths.Find(f => f.Path == path.Path);

                if (match is not null)
                {
                    path.LastSynced = match.LastSynced;
                }
            }

            //Swap and save new config
            _config = newConfig;
            FileSyncUtility.SaveConfig(_config);

            //Begin a new sync regardless of wait time
            lock (_waitIntervalLock)
            {
                Monitor.Pulse(_waitIntervalLock);
            }
        }

        private static Config GetConfig()
        {
            return FileSyncUtility.LoadConfig();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pipeServer?.Dispose();
            }
        }

        public sealed override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
