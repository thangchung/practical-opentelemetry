using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
    public class ValuesController : ControllerBase
    {
        private readonly ILogger<WeatherForecastController> _logger;

        public ValuesController(IHttpClientFactory clientFactory, TracerFactory tracerFactory, ILogger<WeatherForecastController> logger)
        {
            ClientFactory = clientFactory;
            Tracer = tracerFactory.GetTracer("values-controller-tracer");
            _logger = logger;
        }

        public IHttpClientFactory ClientFactory { get; }
        public ITracer Tracer { get; }

        [HttpGet]
        public async Task<IEnumerable<string>> Get()
        {
            //using (TracerFac)
            {
                //var tracer = TracerFac.GetTracer("values-controller-tracer");

                using (Tracer.StartActiveSpan("values-get", out var span))
                {
                    var products = await DoSomething(Tracer);
                    span.End();
                }

                return Enumerable.Range(1, 5).Select(index => index.ToString()).ToArray();
            }
        }

        private async Task<List<Product>> DoSomething(ITracer tracer)
        {
            var client = ClientFactory.CreateClient();
            var clientUrl = "http://localhost:5001/products";

            var outgoingRequest = new HttpRequestMessage(HttpMethod.Get, clientUrl);

            var outgoingSpan = tracer.StartSpan($"HTTP GET Start to call {clientUrl} to get products", SpanKind.Client);

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
