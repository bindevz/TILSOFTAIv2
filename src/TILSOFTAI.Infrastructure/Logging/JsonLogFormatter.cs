using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TILSOFTAI.Domain.Logging;

namespace TILSOFTAI.Infrastructure.Logging
{
    public class JsonLogFormatter
    {
        private readonly JsonSerializerOptions _jsonOptions;

        public JsonLogFormatter()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        public string Format(LogSchema logEntry)
        {
            try
            {
                return JsonSerializer.Serialize(logEntry, _jsonOptions);
            }
            catch (Exception ex)
            {
                // Fallback for serialization errors
                return $"{{\"timestamp\":\"{logEntry.Timestamp}\",\"level\":\"Error\",\"message\":\"Failed to serialize log entry: {ex.Message}\"}}";
            }
        }
    }
}
