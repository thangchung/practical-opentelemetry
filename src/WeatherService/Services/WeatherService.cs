using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Shared;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter.Jaeger;
using System.Net.Http;

namespace WeatherService
{
    public class WeatherServiceImpl : Weather.WeatherBase
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly ILogger<WeatherServiceImpl> _logger;
        private JaegerExporterOptions _jaegerOptions = null;

        public WeatherServiceImpl(IHttpClientFactory clientFactory, IConfiguration config, ILogger<WeatherServiceImpl> logger)
        {
            _jaegerOptions = config.GetOptions<JaegerExporterOptions>("Jaeger");
            _clientFactory = clientFactory;
            _logger = logger;
        }

        public override Task<GetWeathersReply> GetWeathers(GetWeathersRequest request, ServerCallContext context)
        {
            var httpContext = context.GetHttpContext();

            using var tracerFactory = _jaegerOptions.GetTracerFactory();
            var tracer = tracerFactory.GetTracer("weather-service-tracer");
            var propagatedContext = tracer.TextFormat.Extract(httpContext.Request.Headers, (headers, name) => headers[name]);
            var incomingSpan = tracer.StartSpan("gRPC POST get weather data", propagatedContext, SpanKind.Server);

            var rng = new Random();

            var dtos = Enumerable.Range(1, 5).Select(index => new WeatherDto
            {
                TemperatureC = rng.Next(-20, 55),
                Summary = GetSummaryAsync(tracer, incomingSpan).Result // for demo only
            });

            var result = new GetWeathersReply();
            result.Items.AddRange(dtos);
            
            incomingSpan.End();

            return Task.FromResult(result);
        }

        private async Task<string> GetSummaryAsync(ITracer tracer, ISpan span)
        {
            var client = _clientFactory.CreateClient();

            var clientUrl = "http://localhost:5001/summary";

            var outgoingRequest = new HttpRequestMessage(HttpMethod.Get, clientUrl);

            using (tracer.WithSpan(span))
            {
                var outgoingSpan = tracer.StartSpan($"HTTP GET Start to call {clientUrl} to get summary", SpanKind.Client);

                if (outgoingSpan.Context.IsValid)
                {
                    tracer.TextFormat.Inject(
                        outgoingSpan.Context,
                        outgoingRequest.Headers,
                        (headers, name, value) => headers.Add(name, value));
                }

                var result = await client.SendAsync(outgoingRequest);

                outgoingSpan.End();


                return await result.Content.ReadAsStringAsync();
            }
        }
    }
}
