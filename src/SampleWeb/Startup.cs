using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Sampler;
using Prometheus;
using Shared;
using Shared.Kafka;
using Shared.Redis;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleWeb
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            services.AddOpenTelemetry(builder =>
            {
                builder.SetSampler(Samplers.AlwaysSample);

                builder.UseJaeger(o => Configuration.Bind("Jaeger", o));

                builder
                    .UseZipkin(o => Configuration.Bind("Zipkin", o));

                builder.AddRequestCollector()
                    .AddDependencyCollector();
            });

            services.AddHttpClient();

            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            services
                .AddGrpcClient<Meteorite.MeteoriteClient>(o =>
                {
                    o.Address = new Uri(Configuration.GetValue<string>("GrpcServices:MeteoriteConnection"));
                });

            services.Configure<RedisOptions>(o => Configuration.Bind("Redis", o));
            services.AddSingleton<RedisStore>();

            services.AddSingleton<MessageBus>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMetricServer();
            app.UseHttpMetrics();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            Task.Run(() => ConsumeMessage(app));
        }

        private Task ConsumeMessage(IApplicationBuilder appBuilder)
        {
            return appBuilder.ApplicationServices.GetService<MessageBus>()
                .SubscribeAsync<MeteoriteLandingsReply>(message =>
                {
                    using var scope = appBuilder.ApplicationServices.CreateScope();
                    var resolver = scope.ServiceProvider;
                    var traceFactory = resolver.GetService<TracerFactory>();

                    var tracer = traceFactory.GetTracer("kafka-subscriber-tracer");
                    var traceContext = tracer.TextFormat.Extract(message.Headers, (headers, name) => headers
                        .Select(x => x.Key == name ? Encoding.ASCII.GetString(x.GetValueBytes()) : "").ToList());

                    var incomingSpan = tracer.StartSpan("Kafka-subscriber: received data span", traceContext, SpanKind.Server);

                    try
                    {
                        // processing message
                        var here = message;
                        var rng = new Random();
                        var seed = rng.Next(1, 3);
                        if (seed % 2 == 0)
                        {
                            throw new Exception("I threw this exception intently. Yahooo!!!");
                        }
                    }
                    catch (Exception ex)
                    {
                        // commit span
                        var errorSpan = tracer.StartSpan("got error span", incomingSpan, SpanKind.Consumer);
                        errorSpan.AddEvent("Kafka-subscriber-error");
                        errorSpan.PutHttpStatusCode(500, ex.Message);
                        errorSpan.End();

                        // swallow the exception =))
                    }
                    finally
                    {
                        // commit span
                        incomingSpan.End();
                    }
                }, new[] { "coolstore-topic" }, default);
        }
    }
}
