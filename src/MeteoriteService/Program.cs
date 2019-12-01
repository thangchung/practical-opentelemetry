using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Metrics;
using OpenTelemetry.Metrics.Implementation;
using Serilog;
using Serilog.Sinks.Loki;
using Shared.Serilog;
using System;
using System.Collections.Generic;
using System.Net;

namespace MeteoriteService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine(Figgle.FiggleFonts.Standard.Render($"Meteorite Service v0.0.1"));
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    /*var promOptions = new PrometheusExporterOptions() { Url = "http://0.0.0.0:15003/metrics/" };
                        var metric = new Metric<long>("sample");
                        var promExporter = new PrometheusExporter<long>(promOptions, metric);

                        try
                        {
                            promExporter.Start();
                            Log.Logger.Information("Now listening on: http://0.0.0.0:15003");

                            var label1 = new List<KeyValuePair<string, string>>();
                            label1.Add(new KeyValuePair<string, string>("status_code", "200"));
                            var labelSet1 = new LabelSet(label1);
                            metric.GetOrCreateMetricTimeSeries(labelSet1).Add(100);
                        }
                        catch
                        {
                            //promExporter.Stop();
                        }*/

                    webBuilder.ConfigureKestrel((ctx, options) =>
                    {
                        options.Limits.MinRequestBodyDataRate = null;
                        options.Listen(IPAddress.Any, 15003, options => { });
                        options.Listen(IPAddress.Any, 5003, listenOptions =>
                        {
                            listenOptions.Protocols = HttpProtocols.Http2;
                        });
                    });

                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    var seqUrl = hostingContext.Configuration.GetValue<string>("Seq:Connection");

                    var lokiCredentials = new NoAuthCredentials(hostingContext.Configuration.GetValue<string>("Loki:Connection"));

                    Log.Logger = new LoggerConfiguration()
                        .MinimumLevel.Debug()
                        .Enrich.WithProperty("env", hostingContext.HostingEnvironment.EnvironmentName)
                        .Enrich.WithProperty("app", "meteorite-service")
                        .Enrich.FromLogContext()
                        .WriteTo.Console(Serilog.Events.LogEventLevel.Information, "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] [{CorrelationID}] {Message}{NewLine}{Exception}")
                        .WriteTo.Seq(seqUrl)
                        .WriteTo.LokiHttp(lokiCredentials, new LokiLogLabelProvider("meteorite-service", hostingContext.HostingEnvironment.EnvironmentName))
                        .CreateLogger();

                    logging.AddSerilog(dispose: true);
                });
    }
}
