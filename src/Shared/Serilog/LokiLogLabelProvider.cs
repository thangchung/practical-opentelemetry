using Serilog.Sinks.Loki.Labels;
using System;
using System.Collections.Generic;

namespace Shared.Serilog
{
    public class LokiLogLabelProvider : ILogLabelProvider
    {
        public LokiLogLabelProvider(string app, string env)
        {
            App = app ?? throw new ArgumentNullException("App label could not be null");
            Env = env ?? throw new ArgumentNullException("Env label could not be null");
        }

        public string App { get; }
        public string Env { get; }

        public IList<LokiLabel> GetLabels()
        {
            return new List<LokiLabel>
        {
            new LokiLabel("app", App),
            new LokiLabel("environment", Env)
        };
        }
    }
}
