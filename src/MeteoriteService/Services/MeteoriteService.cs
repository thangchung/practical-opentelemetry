using Grpc.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using Shared;
using Shared.Redis;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace MeteoriteService
{
    public class MeteoriteServiceImpl : Meteorite.MeteoriteBase
    {
        private readonly RedisStore _redisStore;
        private readonly IHttpClientFactory _clientFactory;
        private readonly TracerFactory _tracerFactory;
        private readonly ILogger<MeteoriteServiceImpl> _logger;

        public MeteoriteServiceImpl(RedisStore redisStore, IHttpClientFactory clientFactory, TracerFactory tracerFactory, ILogger<MeteoriteServiceImpl> logger)
        {
            _redisStore = redisStore;
            _clientFactory = clientFactory;
            _tracerFactory = tracerFactory;
            _logger = logger;
        }

        public override async Task<MeteoriteLandingsReply> GetMeteoriteLandings(EmptyRequest request, ServerCallContext context)
        {
            var cacheKey = "meteorite-landings";
            var reply = new MeteoriteLandingsReply();

            var tracer = _tracerFactory.GetTracer("GetMeteoriteLandings-tracer");

            var httpContext = context.GetHttpContext();
            var traceContext = tracer.TextFormat.Extract(httpContext.Request.Headers, (headers, name) => headers[name]);
            var incomingSpan = tracer.StartSpan("gRPC POST Received data", traceContext, SpanKind.Server);

            using (tracer.StartActiveSpan("HTTP GET call to remote site to get data", out var span))
            {
                var cacheResult = await _redisStore.RedisCache.StringGetAsync(cacheKey);
                var firstCache = false;

                if (!cacheResult.HasValue)
                {
                    var client = _clientFactory.CreateClient();
                    var clientUrl = "https://data.nasa.gov/resource/y77d-th95.json";
                    var outgoingRequest = new HttpRequestMessage(HttpMethod.Get, clientUrl);

                    var outgoingSpan = tracer.StartSpan($"HTTP GET call to {clientUrl} to get the meteriote data.", SpanKind.Client);
                    if (outgoingSpan.Context.IsValid)
                    {
                        tracer.TextFormat.Inject(
                            outgoingSpan.Context,
                            outgoingRequest.Headers,
                            (headers, name, value) => headers.Add(name, value));
                    }

                    var remoteResult = await client.SendAsync(outgoingRequest);
                    outgoingSpan.End();

                    var meteoriteLandings = new List<MeteoriteLanding>();
                    if (remoteResult.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var stringResult = await remoteResult.Content.ReadAsStringAsync();
                        meteoriteLandings.AddRange(JsonConvert.DeserializeObject<List<MeteoriteLanding>>(stringResult));
                    }
                    else
                    {
                        // get from file offline

                    }

                    await _redisStore.RedisCache.StringSetAsync(cacheKey, JsonConvert.SerializeObject(meteoriteLandings));
                    firstCache = true;
                }

                if (firstCache)
                {
                    cacheResult = await _redisStore.RedisCache.StringGetAsync(cacheKey);
                }

                var list = JsonConvert.DeserializeObject<List<MeteoriteLanding>>(cacheResult.ToString());
                reply.MeteoriteLandings.AddRange(list);
                span.End();
            }

            incomingSpan.End();
            return reply;
        }
    }
}
