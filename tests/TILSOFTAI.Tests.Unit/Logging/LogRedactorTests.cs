using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Infrastructure.Logging;
using Xunit;

namespace TILSOFTAI.Tests.Unit.Logging
{
    public class LogRedactorTests
    {
        private readonly LogRedactor _redactor;

        public LogRedactorTests()
        {
            var options = new LoggingOptions();
            _redactor = new LogRedactor(Options.Create(options));
        }

        [Fact]
        public void Redact_ShouldRedactSensitiveKeys()
        {
            Assert.Equal("[REDACTED]", _redactor.Redact("password", "secret"));
            Assert.Equal("[REDACTED]", _redactor.Redact("apiToken", "abcdef"));
            Assert.Equal("[REDACTED]", _redactor.Redact("connectionString", "Server=..."));
        }

        [Theory]
        [InlineData("username", "john_doe", "john_doe")]
        [InlineData("id", 123, 123)]
        public void Redact_ShouldPassSafeValues(string key, object value, object expected)
        {
            Assert.Equal(expected, _redactor.Redact(key, value));
        }

        [Fact]
        public void Redact_ShouldDetectJwtPattern()
        {
            var jwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";
            var result = _redactor.Redact("someField", jwt);
            Assert.Equal("[Possibly Sensitive - Redacted]", result);
        }
    }
}
