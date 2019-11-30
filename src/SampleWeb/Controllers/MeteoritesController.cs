using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using Shared;
using Shared.Redis;
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
        public MeteoritesController(IHttpClientFactory clientFactory, Meteorite.MeteoriteClient meteoriteClient, 
            TracerFactory tracerFactory, RedisStore redisStore, ILogger<WeatherForecastController> logger)
        {
            ClientFactory = clientFactory;
            TracerFactory = tracerFactory;
            RedisStore = redisStore;
            Logger = logger;
            MeteoriteClient = meteoriteClient;
        }

        public Meteorite.MeteoriteClient MeteoriteClient { get; }
        public IHttpClientFactory ClientFactory { get; }
        public TracerFactory TracerFactory { get; }
        public RedisStore RedisStore { get; }
        public ILogger<WeatherForecastController> Logger { get; }

        [HttpGet("{skip}/{take}")]
        public async Task<IEnumerable<MeteoriteLanding>> Get(int skip, int take)
        {
            var cacheKey = "meteorite-landings";
            var tracer = TracerFactory.GetTracer("meteorites-controller-tracer");

            using (tracer.StartActiveSpan("starts calling to gRPC service span", out var rootSpan))
            {
                var headers = new Metadata();

                var outgoingSpan = tracer.StartSpan($"get meteorite-data span", rootSpan, SpanKind.Client);
                
                if (outgoingSpan.Context.IsValid)
                    tracer.TextFormat.Inject(outgoingSpan.Context, headers, (headers, name, value) => headers.Add(name, value));

                var result = await MeteoriteClient.GetMeteoriteLandingsAsync(
                    new MeteoriteLandingsRequest
                    {
                        Skip = skip,
                        Take = take
                    }, headers);

                outgoingSpan.End();

                var reply = result.MeteoriteLandings.ToList();
                var returnData = new List<MeteoriteLanding>();

                var getCacheSpan = tracer.StartSpan("get data from Redis Cache span", rootSpan);
                var cacheResult = await RedisStore.RedisCache.StringGetAsync(cacheKey);
                if(cacheResult.HasValue)
                {
                    getCacheSpan.AddEvent("on get data from cache event");
                    returnData = JsonConvert.DeserializeObject<List<MeteoriteLanding>>(cacheResult.ToString());
                }

                getCacheSpan.End();
                rootSpan.End();

                return returnData.Count <= 0 ? returnData : returnData.Skip(skip).Take(take);
            }
        }
    }
}
