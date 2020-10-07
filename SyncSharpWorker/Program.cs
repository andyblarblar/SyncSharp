using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SyncSharpWorker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //Change current directory to the binary location instead of system32
            var cwd = Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
            Directory.SetCurrentDirectory(cwd ?? ".");

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();
                    services.AddLogging(conf =>
                    {
                        conf.AddConsole().SetMinimumLevel(LogLevel.Debug);
                    });
                });
    }
}
