using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
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
            _logger.LogInformation("start");

            _pipeServer = new NamedPipeServerStream("syncsharp", PipeDirection.In,
                1);
            
            _config = GetConfig();

            //Start server thread
            Task.Run((async () =>
            {
                await _pipeServer.WaitForConnectionAsync(cancellationToken);
                Memory<byte> buffer = new byte[1080];
                
                while (! cancellationToken.IsCancellationRequested)
                { 
                    await _pipeServer.ReadAsync(buffer, cancellationToken);
                    var newConfig = Serializer.Deserialize<Config>(buffer);

                    //Safely apply the new config
                    SetNewConfig(newConfig);

                    buffer.Span.Clear();
                }

            }), cancellationToken);

            return base.StartAsync(cancellationToken);
        }

  
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            Dispose();

            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                //Wait time between syncs
                lock (_waitIntervalLock)
                {
                    _logger.LogInformation($"entering sleep for {_config.CheckInterval}");
                    Monitor.Wait(_waitIntervalLock, _config.CheckInterval);
                }

                _syncing = true;
                _logger.LogInformation($"woke, starting sync");
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
