using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Logging
{
    public class StructuredLoggerProvider : ILoggerProvider
    {
        private readonly LoggingOptions _options;
        private readonly LogRedactor _redactor;
        private readonly JsonLogFormatter _formatter;
        private readonly ConcurrentDictionary<string, StructuredLogger> _loggers = new();

        public StructuredLoggerProvider(
            IOptions<LoggingOptions> options,
            LogRedactor redactor,
            JsonLogFormatter formatter)
        {
            _options = options.Value;
            _redactor = redactor;
            _formatter = formatter;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new StructuredLogger(name, _options, _redactor, _formatter, Console.WriteLine));
        }

        public void Dispose()
        {
            _loggers.Clear();
        }
    }
}
