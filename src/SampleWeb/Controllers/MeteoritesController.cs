using Grpc.Core;
using Grpc.Net.Client;
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
        private readonly ILogger<WeatherForecastController> _logger;

        private static readonly string GrpcClientUrl = "https://localhost:5003";

        private readonly GrpcChannel _channel = GrpcChannel.ForAddress(GrpcClientUrl);

        public MeteoritesController(IHttpClientFactory clientFactory, TracerFactory tracerFactory, ILogger<WeatherForecastController> logger)
        {
            ClientFactory = clientFactory;
            TracerFactory = tracerFactory;
            MeteoriteClient = new Meteorite.MeteoriteClient(_channel);
            _logger = logger;
        }

        public Meteorite.MeteoriteClient MeteoriteClient { get; }

        public IHttpClientFactory ClientFactory { get; }
        public TracerFactory TracerFactory { get; }

        [HttpGet]
        public async Task<IEnumerable<MeteoriteLanding>> Get()
        {
            var tracer = TracerFactory.GetTracer("MeteoritesController-tracer");

            using (tracer.StartActiveSpan("HTTP GET MeteoriteLanding -> gRPC Service", out var span))
            {
                var headers = new Metadata();

                var outgoingSpan = tracer.StartSpan($"start to call {GrpcClientUrl} to get meteorite-data", SpanKind.Client);
                
                if (outgoingSpan.Context.IsValid)
                    tracer.TextFormat.Inject(outgoingSpan.Context, headers, (headers, name, value) => headers.Add(name, value));
                    
                var result = await MeteoriteClient.GetMeteoriteLandingsAsync(new EmptyRequest(), headers);
                outgoingSpan.End();
                span.End();

                return result.MeteoriteLandings.ToList();
            }
        }
    }
}
