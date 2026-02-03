using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Logging;

namespace TILSOFTAI.Infrastructure.Logging
{
    public class StructuredLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly LoggingOptions _options;
        private readonly LogRedactor _redactor;
        private readonly JsonLogFormatter _formatter;
        private readonly Action<string> _writeLog;

        public StructuredLogger(
            string categoryName,
            LoggingOptions options,
            LogRedactor redactor,
            JsonLogFormatter formatter,
            Action<string> writeLog)
        {
            _categoryName = categoryName;
            _options = options;
            _redactor = redactor;
            _formatter = formatter;
            _writeLog = writeLog;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            // Simple scope support using AsyncLocal is implicit in LogContext handling for this patch,
            // or could support IExternalScopeProvider if needed. 
            // For now, returning null as per minimum viable structured logger that relies on LogContext.
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            // Assuming global enabling, but in a real provider this would check filter rules
            return _options.StructuredLoggingEnabled && logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var logEntry = new LogSchema
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = logLevel.ToString(),
                Message = formatter(state, exception),
                Source = _categoryName,
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                MachineName = Environment.MachineName,
                Context = LogContext.Current.Clone()
            };

            // Enrich with state properties if possible
            if (state is IEnumerable<KeyValuePair<string, object>> properties)
            {
                foreach (var prop in properties)
                {
                    if (prop.Key != "{OriginalFormat}")
                    {
                        var redactedValue = _redactor.Redact(prop.Key, prop.Value);
                        if (redactedValue != null)
                        {
                            logEntry.Properties[prop.Key] = redactedValue;
                        }
                    }
                    else if (prop.Value is string template)
                    {
                        logEntry.MessageTemplate = template;
                    }
                }
            }

            if (exception != null)
            {
                logEntry.Exception = new LogExceptionInfo
                {
                    Type = exception.GetType().FullName ?? "Unknown",
                    Message = exception.Message,
                    StackTrace = exception.StackTrace
                };
            }

            string output;
            if (_options.OutputFormat == LogOutputFormat.Json)
            {
                output = _formatter.Format(logEntry);
            }
            else
            {
                // Fallback to simple console format
                output = $"[{logEntry.Timestamp:HH:mm:ss} {logEntry.Level}] {logEntry.Source}: {logEntry.Message}";
                if (exception != null)
                {
                    output += Environment.NewLine + exception.ToString();
                }
            }

            _writeLog(output);
        }
    }
}
