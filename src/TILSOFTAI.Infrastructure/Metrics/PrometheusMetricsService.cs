using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Prometheus;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Metrics;
using Microsoft.Extensions.Options;

namespace TILSOFTAI.Infrastructure.Metrics
{
    public class PrometheusMetricsService : IMetricsService
    {
        private readonly MetricsOptions _options;
        private readonly ConcurrentDictionary<string, Counter> _counters = new();
        private readonly ConcurrentDictionary<string, Gauge> _gauges = new();
        private readonly ConcurrentDictionary<string, Histogram> _histograms = new();

        public PrometheusMetricsService(IOptions<MetricsOptions> options)
        {
            _options = options.Value;
        }

        public void IncrementCounter(string name, Dictionary<string, string>? labels = null, double value = 1.0)
        {
            if (!_options.Enabled) return;

            var counter = _counters.GetOrAdd(name, n => Prometheus.Metrics.CreateCounter(n, n, GetLabelNames(labels)));
            
            if (labels != null && labels.Count > 0)
            {
                counter.WithLabels(GetLabelValues(labels)).Inc(value);
            }
            else
            {
                counter.Inc(value);
            }
        }

        public void RecordGauge(string name, double value, Dictionary<string, string>? labels = null)
        {
            if (!_options.Enabled) return;

            var gauge = _gauges.GetOrAdd(name, n => Prometheus.Metrics.CreateGauge(n, n, GetLabelNames(labels)));

            if (labels != null && labels.Count > 0)
            {
                gauge.WithLabels(GetLabelValues(labels)).Set(value);
            }
            else
            {
                gauge.Set(value);
            }
        }

        public void RecordHistogram(string name, double value, Dictionary<string, string>? labels = null)
        {
            if (!_options.Enabled) return;

            var histogram = _histograms.GetOrAdd(name, n => Prometheus.Metrics.CreateHistogram(n, n, new HistogramConfiguration
            {
                LabelNames = GetLabelNames(labels),
                Buckets = _options.HistogramBuckets
            }));

            if (labels != null && labels.Count > 0)
            {
                histogram.WithLabels(GetLabelValues(labels)).Observe(value);
            }
            else
            {
                histogram.Observe(value);
            }
        }

        public IDisposable CreateTimer(string name, Dictionary<string, string>? labels = null)
        {
             if (!_options.Enabled) return new NoOpDisposable();

            var histogram = _histograms.GetOrAdd(name, n => Prometheus.Metrics.CreateHistogram(n, n, new HistogramConfiguration
            {
                LabelNames = GetLabelNames(labels),
                Buckets = _options.HistogramBuckets
            }));

            Prometheus.ITimer timer;
            if (labels != null && labels.Count > 0)
            {
                timer = histogram.WithLabels(GetLabelValues(labels)).NewTimer();
            }
            else
            {
                timer = histogram.NewTimer();
            }

            return timer;
        }

        private string[] GetLabelNames(Dictionary<string, string>? labels)
        {
            if (labels == null || labels.Count == 0) return Array.Empty<string>();
            
            // Ensure consistent order
            return labels.Keys.OrderBy(k => k).ToArray();
        }

        private string[] GetLabelValues(Dictionary<string, string> labels)
        {
            // Ensure consistent order matching names
            return labels.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToArray();
        }

        private class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
