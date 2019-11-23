using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Sinks.Loki;
using Shared.Serilog;

namespace SampleWeb
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    var seqUrl = hostingContext.Configuration.GetValue<string>("Seq:Connection");

                    var lokiCredentials = new NoAuthCredentials(hostingContext.Configuration.GetValue<string>("Loki:Connection"));

                    Log.Logger = new LoggerConfiguration()
                        .MinimumLevel.Debug()
                        .Enrich.WithProperty("Environment", hostingContext.HostingEnvironment.EnvironmentName)
                        .Enrich.WithProperty("Microservices", "SampleWeb")
                        .Enrich.FromLogContext()
                        .WriteTo.Console(Serilog.Events.LogEventLevel.Information, "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] [{CorrelationID}] {Message}{NewLine}{Exception}")
                        .WriteTo.Seq(seqUrl)
                        .WriteTo.LokiHttp(lokiCredentials, new LokiLogLabelProvider("sample-web", hostingContext.HostingEnvironment.EnvironmentName))
                        .CreateLogger();

                    logging.AddSerilog(dispose: true);
                });
    }
}
