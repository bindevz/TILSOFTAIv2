using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Logging
{
    public class LogRedactor : ILogRedactor
    {
        private readonly LoggingOptions _options;
        private readonly Regex _sensitiveValuePattern;

        public LogRedactor(IOptions<LoggingOptions> options)
        {
            _options = options.Value;
            // Matches values that look like JWTs, API keys, etc. (simplified)
            // JWT: header.payload.signature
            // API Key: commonly 32+ alnum chars
            _sensitiveValuePattern = new Regex(@"(eyJ[a-zA-Z0-9\-_]+\.[a-zA-Z0-9\-_]+\.[a-zA-Z0-9\-_]+)|([a-zA-Z0-9]{32,})", RegexOptions.Compiled);
        }

        public object? Redact(string key, object? value)
        {
            if (value == null) return null;

            if (IsSensitiveKey(key))
            {
                return "[REDACTED]";
            }

            if (value is string s)
            {
                if (_sensitiveValuePattern.IsMatch(s))
                {
                    // If it looks like a token/secret, mask it partially or fully
                    return "[Possibly Sensitive - Redacted]";
                }
                
                if (s.Length > _options.MaxPropertyValueLength)
                {
                    return s.Substring(0, _options.MaxPropertyValueLength) + "... [TRUNCATED]";
                }
            }

            return value;
        }

        private bool IsSensitiveKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            
            foreach (var redactedField in _options.RedactedFields)
            {
                if (key.IndexOf(redactedField, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
