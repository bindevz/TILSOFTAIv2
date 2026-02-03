using System;
using System.Collections.Generic;

namespace TILSOFTAI.Domain.Metrics
{
    public interface IMetricsService
    {
        void IncrementCounter(string name, Dictionary<string, string>? labels = null, double value = 1.0);
        void RecordHistogram(string name, double value, Dictionary<string, string>? labels = null);
        void RecordGauge(string name, double value, Dictionary<string, string>? labels = null);
        IDisposable CreateTimer(string name, Dictionary<string, string>? labels = null);
    }
}
