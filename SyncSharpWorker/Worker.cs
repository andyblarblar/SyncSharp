using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;
using ProtoBuf;
using SyncSharp.Common;
using SyncSharp.Common.model;

namespace SyncSharpWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        private NamedPipeServerStream pipeServer;

        private Config config;

        /// <summary>
        /// Token merged with IHost token. 
        /// </summary>
        private CancellationTokenSource cts;
        private readonly object _monitorLock = new object();

        private event Action ConfigChanged;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            ConfigChanged += () =>
            {
                _logger.LogDebug(
                    "new config received, canceling sync"); //TODO cancellation may cause it to be permanently cancelled
                cts.Cancel(); //Stop any syncs in progress

                //Wakes the main thread if waiting between syncs
                lock (_monitorLock)
                {
                    Monitor.Pulse(_monitorLock);
                }
                //The program should now reenter the waiting loop in the main thread
            };

            _logger.LogInformation("start");

            pipeServer = new NamedPipeServerStream("syncsharp", PipeDirection.In,
                1, PipeTransmissionMode.Message);
          
            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            config = GetConfig();

            //Start server thread
            Task.Run((async () =>
            {
                await pipeServer.WaitForConnectionAsync(cancellationToken);
                Memory<byte> buffer = new byte[1080];
                
                while (! cancellationToken.IsCancellationRequested)
                { 
                    await pipeServer.ReadAsync(buffer, cancellationToken);
                    var newConfig = Serializer.Deserialize<Config>(buffer);

                    //Migrate in memory sync times to the new config so we don't lose them 
                    foreach (var path in newConfig.Paths)
                    {
                        var match = config.Paths.Find(f => f.Path == path.Path);

                        if (match is not null)
                        {
                            path.LastSynced = match.LastSynced;
                        }
                    }

                    config = newConfig;

                    OnConfigChanged();
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
                lock (_monitorLock)
                {
                    _logger.LogInformation($"entering sleep for {config.CheckInterval}");
                    Monitor.Wait(_monitorLock, config.CheckInterval);
                }

                _logger.LogInformation($"woke, starting sync");
                await FileSyncUtility.Sync(config, cts.Token,_logger);
            }
        }

        private Config GetConfig()
        {
            //TODO in the future check a registy var, then deserialise 
            
            return new Config
            {
                CheckInterval = TimeSpan.FromMinutes(.5),
                Paths = new List<FileProfile>{new FileProfile{LastSynced = DateTime.MinValue, Path = "Y:\\Documents\\School" } },
                SavePath = "C:\\Users\\Andyblarblar\\Downloads\\Backu"
            };

        }

        protected virtual void OnConfigChanged()
        {
            ConfigChanged?.Invoke();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                pipeServer?.Dispose();
                cts?.Dispose();
            }
        }

        public sealed override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
