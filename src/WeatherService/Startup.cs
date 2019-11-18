using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Exporter.Jaeger;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using OpenTelemetry.Trace.Sampler;
using Shared;

namespace WeatherService
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
            services.AddGrpc();

            services.AddOpenTelemetry(builder =>
            {
                builder.SetSampler(Samplers.AlwaysSample);

                builder.AddProcessorPipeline(c => c
                    .SetExporter(new JaegerTraceExporter(Configuration.GetOptions<JaegerExporterOptions>("Jaeger")))
                    .SetExportingProcessor(e => new BatchingSpanProcessor(e)));

                builder
                    .UseZipkin(o => Configuration.Bind("Zipkin", o));

                builder.AddRequestCollector()
                    .AddDependencyCollector();
            });

            services.AddScoped(resolver => resolver.GetService<TracerFactory>().GetTracer("weather-service-tracer"));

            services.AddHttpClient();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                GrpcEndpointRouteBuilderExtensions.MapGrpcService<WeatherServiceImpl>(endpoints);

                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
                });
            });
        }
    }
}
