using System.Threading;

namespace TILSOFTAI.Domain.Logging
{
    public class LogContext
    {
        private static readonly AsyncLocal<LogContext> _current = new();

        public static LogContext Current
        {
            get => _current.Value ??= new LogContext();
            set => _current.Value = value;
        }

        public string? TenantId { get; set; }
        public string? UserId { get; set; }
        public string? CorrelationId { get; set; }
        public string? ConversationId { get; set; }
        public string? RequestId { get; set; }
        public string? TraceId { get; set; }
        public string? SpanId { get; set; }

        public LogContext Clone()
        {
            return new LogContext
            {
                TenantId = TenantId,
                UserId = UserId,
                CorrelationId = CorrelationId,
                ConversationId = ConversationId,
                RequestId = RequestId,
                TraceId = TraceId,
                SpanId = SpanId
            };
        }
    }
}
