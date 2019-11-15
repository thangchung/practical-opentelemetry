using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenTelemetry.Exporter.Jaeger;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
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
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };
        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(IHttpClientFactory clientFactory, ILogger<WeatherForecastController> logger)
        {
            ClientFactory = clientFactory;
            _logger = logger;
        }

        public IHttpClientFactory ClientFactory { get; }

        [HttpGet]
        public async Task<IEnumerable<WeatherForecast>> Get()
        {
            var jaegerOptions = new JaegerExporterOptions()
            {
                ServiceName = "sample-web",
                AgentHost = "localhost",
                AgentPort = 6831,
            };

            using var tracerFactory = TracerFactory.Create(builder => builder
                .AddProcessorPipeline(c => c
                    .SetExporter(new JaegerTraceExporter(jaegerOptions))));

            var tracer = tracerFactory.GetTracer("sample-web-tracer");

            using (tracer.StartActiveSpan("weatherforecast-get", out var span))
            {
                var rng = new Random();

                var products = await DoSomething(tracer);

                return Enumerable.Range(1, 5).Select(index => new WeatherForecast
                {
                    
                    Date = DateTime.Now.AddDays(index),
                    TemperatureC = rng.Next(-20, 55),
                    Summary = Summaries[rng.Next(Summaries.Length)]
                })
                .ToArray();
            }
        }

        private async Task<List<Product>> DoSomething(ITracer tracer)
        {
            var client = ClientFactory.CreateClient();
            var clientUrl = "http://localhost:5001/products";

            var outgoingRequest = new HttpRequestMessage(HttpMethod.Get, clientUrl);

            var outgoingSpan = tracer.StartSpan($"Start to call {clientUrl} to get products", SpanKind.Client);

            if (outgoingSpan.Context.IsValid)
            {
                tracer.TextFormat.Inject(
                    outgoingSpan.Context,
                    outgoingRequest.Headers,
                    (headers, name, value) => headers.Add(name, value));
            }

            var jsonData = await client.SendAsync(outgoingRequest);

            outgoingSpan.End();

            var products = JsonConvert.DeserializeObject<List<Product>>(await jsonData.Content.ReadAsStringAsync());

            return products;
        }
    }

    public class Product
    {
        public Guid Id { get; set; }

        public string Name { get; set; }
    }
}
