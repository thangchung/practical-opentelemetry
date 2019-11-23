using System;
using OpenTelemetry.Exporter.Jaeger;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;

namespace Shared
{
    /*public static class JaegerTraceBuilderExtensions
    {
        /// <summary>Registers Jaeger exporter.</summary>
        /// <param name="builder">Trace builder to use.</param>
        /// <param name="configure">Configuration options.</param>
        /// <returns>The instance of <see cref="T:OpenTelemetry.Trace.Configuration.TracerBuilder" /> to chain the calls.</returns>
        public static TracerBuilder UseJaeger(
            this TracerBuilder builder,
            Action<JaegerExporterOptions> configure)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            var options = new JaegerExporterOptions();
            configure(options);

            return builder.AddProcessorPipeline(b => b
                .SetExporter(new JaegerTraceExporter(options, null))
                .SetExportingProcessor(e => (SpanProcessor)new BatchingSpanProcessor(e)));
        }
    }*/
}
