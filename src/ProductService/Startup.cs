using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using OpenTelemetry.Exporter.Jaeger;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using OpenTelemetry.Trace.Sampler;
using Shared;
using System;
using System.Collections.Generic;

namespace ProductService
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

            services.AddScoped(resolver => resolver.GetService<TracerFactory>().GetTracer("product-service-tracer"));
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
                endpoints.MapGet("/products", async httpContext =>
                {
                    var jaegerOptions = new JaegerExporterOptions()
                    {
                        ServiceName = "product-service",
                        AgentHost = "localhost",
                        AgentPort = 6831,
                    };

                    using var tracerFactory = TracerFactory.Create(builder => builder
                        .AddProcessorPipeline(c => c
                            .SetExporter(new JaegerTraceExporter(jaegerOptions))));

                    var tracer = tracerFactory.GetTracer("product-service-tracer");

                    var context = tracer.TextFormat.Extract(httpContext.Request.Headers, (headers, name) => headers[name]);

                    var incomingSpan = tracer.StartSpan("HTTP GET get products", context, SpanKind.Server);

                    var products = new List<Product>();

                    products.Add(new Product
                    {
                        Id = Guid.NewGuid(),
                        Name = "Product Name"
                    });

                    incomingSpan.SetAttribute("products", JsonConvert.SerializeObject(products));

                    incomingSpan.End();

                    await httpContext.Response.WriteAsync(JsonConvert.SerializeObject(products));
                });

                endpoints.MapGet("/summary", async httpContext =>
                {
                    var config = httpContext.RequestServices.GetService<IConfiguration>();

                    var jaegerOptions = config.GetOptions<JaegerExporterOptions>("Jaeger");

                    using var tracerFactory = jaegerOptions.GetTracerFactory();

                    var tracer = tracerFactory.GetTracer("product-service-tracer");

                    var context = tracer.TextFormat.Extract(httpContext.Request.Headers, (headers, name) => headers[name]);

                    var incomingSpan = tracer.StartSpan("HTTP GET get summary", context, SpanKind.Server);

                    string[] Summaries = new[]
                    {
                        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
                    };

                    var rng = new Random();

                    var summary = Summaries[rng.Next(Summaries.Length)];

                    incomingSpan.End();

                    await httpContext.Response.WriteAsync(summary);
                });
            });
        }
    }

    public class Product
    {
        public Guid Id { get; set; }

        public string Name { get; set; }
    }
}
