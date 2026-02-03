using System;
using System.Collections.Generic;

namespace TILSOFTAI.Domain.Logging
{
    public class LogSchema
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? MessageTemplate { get; set; }
        public LogExceptionInfo? Exception { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
        public LogContext? Context { get; set; }
        public string? Source { get; set; }
        public string Application { get; set; } = "TILSOFTAI";
        public string Environment { get; set; } = "Production";
        public string? MachineName { get; set; }
    }

    public class LogExceptionInfo
    {
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
    }
}
