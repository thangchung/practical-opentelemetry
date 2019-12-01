using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.Jaeger;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SampleWeb.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private readonly ILogger<WeatherForecastController> _logger;

        private JaegerExporterOptions jaegerOptions = null; 

        public WeatherForecastController(IHttpClientFactory clientFactory, IConfiguration config, ILogger<WeatherForecastController> logger)
        {
            ClientFactory = clientFactory;
            jaegerOptions = config.GetOptions<JaegerExporterOptions>("Jaeger");
            _logger = logger;
        }

        public IHttpClientFactory ClientFactory { get; }
        public TracerFactory TracerFac => jaegerOptions.GetTracerFactory();
        public IConfiguration Configuration { get; }

        [HttpGet]
        public async Task<IEnumerable<WeatherForecast>> Get()
        {
            using var tracerFactory = TracerFac;

            var tracer = tracerFactory.GetTracer("sample-web-tracer");

            using (tracer.StartActiveSpan("weatherforecast-get", out var span))
            {
                var rng = new Random();

                var weatherData = await GetWeatherData(tracer);

                return weatherData.Select(prod => new WeatherForecast
                {
                    Date = DateTime.Now.AddDays(rng.Next(1, 10)),
                    TemperatureC = prod.TemperatureC,
                    Summary = prod.Summary
                })
                .ToArray();
            }
        }

        private async Task<List<WeatherDto>> GetWeatherData(ITracer tracer)
        {
            var clientUrl = "https://localhost:5002";
            var channel = GrpcChannel.ForAddress(clientUrl);
            var client = new Weather.WeatherClient(channel);
            
            var headers = new Metadata();

            var outgoingSpan = tracer.StartSpan($"Start to call {clientUrl} to get weather data", SpanKind.Client);

            if (outgoingSpan.Context.IsValid)
                tracer.TextFormat.Inject(outgoingSpan.Context, headers, (headers, name, value) => headers.Add(name, value));

            var result = await client.GetWeathersAsync(new GetWeathersRequest(), headers);

            outgoingSpan.End();

            return result.Items.ToList();
        }
    }

    public class WeatherForecast
    {
        public DateTime Date { get; set; }

        public int TemperatureC { get; set; }

        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

        public string Summary { get; set; }
    }
}
