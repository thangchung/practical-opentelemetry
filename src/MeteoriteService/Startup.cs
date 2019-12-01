using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Collector.StackExchangeRedis;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Sampler;
using Prometheus;
using Shared.Kafka;
using Shared.Redis;

namespace MeteoriteService
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

            services.Configure<RedisOptions>(o => Configuration.Bind("Redis", o));
            services.AddSingleton<RedisStore>();

            services.AddSingleton<MessageBus>();

            services.AddGrpc();

            services.AddOpenTelemetry((resolver, builder) =>
            {
                builder.SetSampler(Samplers.AlwaysSample);

                builder.UseJaeger(o => Configuration.Bind("Jaeger", o));

                builder
                    .UseZipkin(o => Configuration.Bind("Zipkin", o));

                builder
                    .AddCollector(c =>
                    {
                        using var scope = resolver.CreateScope();
                        var provider = scope.ServiceProvider;
                        var connection = provider.GetService<RedisStore>()?.Connection;
                        var collector = new StackExchangeRedisCallsCollector(c);
                        connection.RegisterProfiler(collector.GetProfilerSessionsFactory());
                        return collector;
                    })
                    .AddRequestCollector()
                    .AddDependencyCollector();
            });

            services.AddHttpClient();
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

            app.UseEndpoints(endpoints =>
            {
                GrpcEndpointRouteBuilderExtensions.MapGrpcService<MeteoriteServiceImpl>(endpoints);

                endpoints.MapControllers();

                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
                });
            });
        }
    }
}
