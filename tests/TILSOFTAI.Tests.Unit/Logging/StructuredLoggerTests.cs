using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Infrastructure.Logging;
using Xunit;

namespace TILSOFTAI.Tests.Unit.Logging
{
    public class StructuredLoggerTests
    {
        private readonly LoggingOptions _options;
        private readonly LogRedactor _redactor;
        private readonly JsonLogFormatter _formatter;
        private readonly List<string> _loggedMessages;
        private readonly StructuredLogger _logger;

        public StructuredLoggerTests()
        {
            _options = new LoggingOptions { OutputFormat = LogOutputFormat.Json };
            _redactor = new LogRedactor(Options.Create(_options));
            _formatter = new JsonLogFormatter();
            _loggedMessages = new List<string>();
            _logger = new StructuredLogger("TestCategory", _options, _redactor, _formatter, msg => _loggedMessages.Add(msg));
        }

        [Fact]
        public void Log_ShouldSerializeToJson()
        {
            _logger.LogInformation("Test message");

            Assert.Single(_loggedMessages);
            Assert.Contains("\"message\":\"Test message\"", _loggedMessages[0]);
            Assert.Contains("\"level\":\"Information\"", _loggedMessages[0]);
            Assert.Contains("\"source\":\"TestCategory\"", _loggedMessages[0]);
        }

        [Fact]
        public void Log_ShouldRedactSensitiveProperties()
        {
            _logger.LogInformation("User created {password}", "secret123");

            Assert.Single(_loggedMessages);
            var log = _loggedMessages[0];
            Assert.Contains("[REDACTED]", log);
            Assert.DoesNotContain("secret123", log);
        }

        [Fact]
        public void Log_ShouldIncludeExceptionDetails()
        {
            var ex = new InvalidOperationException("Failure");
            _logger.LogError(ex, "Something went wrong");

            Assert.Single(_loggedMessages);
            var log = _loggedMessages[0];
            Assert.Contains("\"exception\":{", log);
            Assert.Contains("\"type\":\"System.InvalidOperationException\"", log);
            Assert.Contains("\"message\":\"Failure\"", log);
        }
    }
}
