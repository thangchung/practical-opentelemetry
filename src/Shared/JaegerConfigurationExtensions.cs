using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter.Jaeger;
using OpenTelemetry.Trace.Configuration;

namespace Shared
{
    public static class JaegerConfigurationExtensions
    {
        public static TracerFactory GetTracerFactory(this JaegerExporterOptions options)
        {
            var tracerFactory = TracerFactory.Create(builder => builder
                .AddProcessorPipeline(c => c
                    .SetExporter(new JaegerTraceExporter(options))));

            return tracerFactory;
        }

        public static TModel GetOptions<TModel>(this IConfiguration configuration, string section) where TModel : new()
        {
            var model = new TModel();
            configuration.GetSection(section).Bind(model);
            return model;
        }
    }
}
