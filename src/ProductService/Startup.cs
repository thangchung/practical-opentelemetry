using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using OpenTelemetry.Exporter.Jaeger;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using System;
using System.Collections.Generic;

namespace ProductService
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOpenTelemetry();
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
            });
        }
    }

    public class Product
    {
        public Guid Id { get; set; }

        public string Name { get; set; }
    }
}
