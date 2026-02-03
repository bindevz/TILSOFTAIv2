using System.Collections.Generic;

namespace TILSOFTAI.Domain.Configuration
{
    public class MetricsOptions
    {
        public bool Enabled { get; set; } = true;
        public string EndpointPath { get; set; } = "/metrics";
        public bool RequireAuthentication { get; set; } = false;
        public string[] AllowedRoles { get; set; } = System.Array.Empty<string>();
        public bool IncludeTenantLabel { get; set; } = true;
        public int MaxLabelCardinality { get; set; } = 100;
        public double[] HistogramBuckets { get; set; } = new[] { 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10 };
        public bool EnableRuntimeMetrics { get; set; } = true;
    }
}
