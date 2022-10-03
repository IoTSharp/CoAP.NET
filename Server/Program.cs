using System;

namespace Server
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NLog.Extensions.Logging;
    using WorldDirect.CoAP;

    /// <summary>
    /// Represents the application startup configuration.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        /// <param name="args">The arguments.</param>
        public static void Main(string[] args)
        {
            var rootLogger = NLog.LogManager.LoadConfiguration("./Config/NLog.config").GetCurrentClassLogger();
            try
            {
                rootLogger.Trace("Application started");
                CreateHostBuilder(args)
                    .Build()
                    .Run();
            }
            catch (Exception e)
            {
                rootLogger.Error(e, "Application crashed.");
                throw;
            }
            finally
            {
                rootLogger.Trace("Application finished.");
                NLog.LogManager.Shutdown();
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("Config/appsettings.json", false);
                    config.AddJsonFile($"Config/appsettings.{context.HostingEnvironment.EnvironmentName}.json", true);
                })
                .ConfigureLogging(log =>
                {
                    log.ClearProviders();
                    log.SetMinimumLevel(LogLevel.Trace);
                    log.AddNLog();
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddHostedService<CoapServerService>();
                    services.AddTransient<CoapServer>();
                    services.UseRFC7252Specification();
                    services.AddOptionFactories(typeof(Oscore.OscoreOptionFactory).Assembly);
                });
        }
    }
}
