using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using Shared;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SampleWeb.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MeteoritesController : ControllerBase
    {
        public MeteoritesController(IHttpClientFactory clientFactory, Meteorite.MeteoriteClient meteoriteClient, TracerFactory tracerFactory, ILogger<WeatherForecastController> logger)
        {
            ClientFactory = clientFactory;
            TracerFactory = tracerFactory;
            Logger = logger;
            MeteoriteClient = meteoriteClient;
        }

        public Meteorite.MeteoriteClient MeteoriteClient { get; }
        public IHttpClientFactory ClientFactory { get; }
        public TracerFactory TracerFactory { get; }
        public ILogger<WeatherForecastController> Logger { get; }

        [HttpGet("{skip}/{take}")]
        public async Task<IEnumerable<MeteoriteLanding>> Get(int skip, int take)
        {
            var tracer = TracerFactory.GetTracer("MeteoritesController-tracer");

            using (tracer.StartActiveSpan("HTTP GET MeteoriteLanding -> gRPC Service", out var span))
            {
                var headers = new Metadata();

                var outgoingSpan = tracer.StartSpan($"HTTP POST get meteorite-data", SpanKind.Client);
                
                if (outgoingSpan.Context.IsValid)
                    tracer.TextFormat.Inject(outgoingSpan.Context, headers, (headers, name, value) => headers.Add(name, value));

                var result = await MeteoriteClient.GetMeteoriteLandingsAsync(
                    new MeteoriteLandingsRequest
                    {
                        Skip = skip,
                        Take = take
                    }, headers);

                outgoingSpan.End();
                span.End();

                return result.MeteoriteLandings.ToList();
            }
        }
    }
}
