using Confluent.Kafka;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using Shared;
using Shared.Kafka;
using Shared.Redis;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MeteoriteService
{
    public class MeteoriteServiceImpl : Meteorite.MeteoriteBase
    {
        private readonly RedisStore _redisStore;
        private readonly MessageBus _messageBus;
        private readonly IHttpClientFactory _clientFactory;
        private readonly TracerFactory _tracerFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MeteoriteServiceImpl> _logger;

        public MeteoriteServiceImpl(RedisStore redisStore, MessageBus messageBus, IHttpClientFactory clientFactory, 
            TracerFactory tracerFactory, IConfiguration configuration, ILogger<MeteoriteServiceImpl> logger)
        {
            _redisStore = redisStore;
            _messageBus = messageBus;
            _clientFactory = clientFactory;
            _tracerFactory = tracerFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public override async Task<MeteoriteLandingsReply> GetMeteoriteLandings(MeteoriteLandingsRequest request, ServerCallContext context)
        {
            var nasaService = _configuration.GetValue<string>("ExternalServices:NasaMeteoriteService");
            var cacheKey = "meteorite-landings";
            var reply = new MeteoriteLandingsReply();

            var tracer = _tracerFactory.GetTracer("get-meteorite-landings-tracer");

            var httpContext = context.GetHttpContext();
            var traceContext = tracer.TextFormat.Extract(httpContext.Request.Headers, (headers, name) => headers[name]);
            var incomingSpan = tracer.StartSpan("received the request span", traceContext, SpanKind.Server);
            
            using (tracer.StartActiveSpan("call to NASA site root span", incomingSpan, out var rootSpan))
            {
                var cacheResult = await _redisStore.RedisCache.StringGetAsync(cacheKey);
                var firstCache = false;

                if (!cacheResult.HasValue)
                {
                    var client = _clientFactory.CreateClient();
                    var outgoingRequest = new HttpRequestMessage(HttpMethod.Get, nasaService);

                    var outgoingSpan = tracer.StartSpan($"call to {nasaService} span", rootSpan, SpanKind.Client);
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
                        // TODO: get from file offline
                        //...
                    }

                    await _redisStore.RedisCache.StringSetAsync(cacheKey, JsonConvert.SerializeObject(meteoriteLandings));
                    firstCache = true;
                }

                if (firstCache)
                {
                    cacheResult = await _redisStore.RedisCache.StringGetAsync(cacheKey);
                }

                var list = JsonConvert.DeserializeObject<List<MeteoriteLanding>>(cacheResult.ToString());
                reply.MeteoriteLandings.AddRange(list.Skip(request.Skip).Take(request.Take).Select(x => x.Id).ToArray());

                // publish it
                var messageHeaders = new Headers();
                var outgoingKafkaSpan = tracer.StartSpan($"publish message to Kafka span", rootSpan, SpanKind.Producer);
                if (outgoingKafkaSpan.Context.IsValid)
                {
                    tracer.TextFormat.Inject(
                        outgoingKafkaSpan.Context,
                        messageHeaders,
                        (headers, name, value) => headers.Add(new Header(name, Encoding.ASCII.GetBytes(value))));
                }

                await _messageBus.PublishAsync(reply, messageHeaders, new[] { "coolstore-topic" });
                
                outgoingKafkaSpan.End();
                incomingSpan.End();
                rootSpan.End();
            }

            return reply;
        }
    }
}
