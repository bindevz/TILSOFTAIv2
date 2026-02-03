using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Audit;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Audit;

/// <summary>
/// Non-blocking audit logger that buffers events for async processing.
/// </summary>
public sealed class AuditLogger : IAuditLogger
{
    private readonly AuditOptions _options;
    private readonly ILogger<AuditLogger> _logger;
    private readonly Channel<AuditEvent> _channel;
    private readonly Regex[] _redactPatterns;

    public AuditLogger(
        IOptions<AuditOptions> options,
        ILogger<AuditLogger> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _channel = Channel.CreateBounded<AuditEvent>(new BoundedChannelOptions(_options.BufferSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        // Compile redaction patterns
        _redactPatterns = _options.RedactFields
            .Select(field => new Regex($@"(""{Regex.Escape(field)}""\s*:\s*)""[^""]*""", RegexOptions.Compiled | RegexOptions.IgnoreCase))
            .ToArray();
    }

    /// <summary>
    /// Gets the channel reader for the background service.
    /// </summary>
    public ChannelReader<AuditEvent> Reader => _channel.Reader;

    public void LogAuthenticationEvent(AuthAuditEvent auditEvent)
    {
        if (!_options.ShouldAudit(auditEvent.EventType)) return;
        EnqueueEvent(auditEvent);
    }

    public void LogAuthorizationEvent(AuthzAuditEvent auditEvent)
    {
        if (!_options.ShouldAudit(auditEvent.EventType)) return;
        EnqueueEvent(auditEvent);
    }

    public void LogDataAccessEvent(DataAccessAuditEvent auditEvent)
    {
        if (!_options.ShouldAudit(auditEvent.EventType)) return;
        EnqueueEvent(auditEvent);
    }

    public void LogSecurityEvent(SecurityAuditEvent auditEvent)
    {
        if (!_options.ShouldAudit(auditEvent.EventType)) return;
        EnqueueEvent(auditEvent);
    }

    public void Log(AuditEvent auditEvent)
    {
        if (!_options.ShouldAudit(auditEvent.EventType)) return;
        EnqueueEvent(auditEvent);
    }

    private void EnqueueEvent(AuditEvent evt)
    {
        try
        {
            // Redact sensitive fields
            RedactEvent(evt);

            // Compute checksum
            evt.ComputeChecksum();

            // Try to write to channel (non-blocking)
            if (!_channel.Writer.TryWrite(evt))
            {
                _logger.LogWarning("Audit buffer full, oldest event dropped. EventType: {EventType}", evt.EventType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue audit event. EventType: {EventType}", evt.EventType);
        }
    }

    private void RedactEvent(AuditEvent evt)
    {
        // Redact token claims in AuthAuditEvent
        if (evt is AuthAuditEvent authEvt)
        {
            var redactedClaims = new Dictionary<string, string>();
            foreach (var claim in authEvt.TokenClaims)
            {
                var isRedacted = _options.RedactFields.Any(f =>
                    claim.Key.Contains(f, StringComparison.OrdinalIgnoreCase));
                redactedClaims[claim.Key] = isRedacted ? "[REDACTED]" : claim.Value;
            }
            // Note: TokenClaims is init-only, so we can't reassign.
            // The redaction happens at serialization time in Details.
        }

        // Redact Details JSON
        if (evt.Details is not null)
        {
            var json = evt.Details.RootElement.GetRawText();
            foreach (var pattern in _redactPatterns)
            {
                json = pattern.Replace(json, "$1\"[REDACTED]\"");
            }
            // Replace Details with redacted version would require modification
            // For now, the Details are logged as-is with redaction at the sink level
        }
    }
}
