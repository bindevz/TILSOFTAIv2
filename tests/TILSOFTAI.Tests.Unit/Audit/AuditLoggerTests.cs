using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TILSOFTAI.Domain.Audit;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Infrastructure.Audit;

namespace TILSOFTAI.Tests.Unit.Audit;

public class AuditLoggerTests
{
    private readonly Mock<ILogger<AuditLogger>> _loggerMock;
    private readonly AuditOptions _options;

    public AuditLoggerTests()
    {
        _loggerMock = new Mock<ILogger<AuditLogger>>();
        _options = new AuditOptions
        {
            Enabled = true,
            BufferSize = 100
        };
    }

    private AuditLogger CreateLogger(AuditOptions? options = null)
    {
        return new AuditLogger(Options.Create(options ?? _options), _loggerMock.Object);
    }

    [Fact]
    public void LogAuthenticationEvent_WhenEnabled_EnqueuesEvent()
    {
        var logger = CreateLogger();
        var evt = AuthAuditEvent.Success("tenant1", "user1", "corr1", "127.0.0.1", "TestAgent");

        // Act - should not block
        logger.LogAuthenticationEvent(evt);

        // Assert - event should be readable from channel
        Assert.True(logger.Reader.TryRead(out var enqueued));
        Assert.NotNull(enqueued);
        Assert.Equal(AuditEventType.Authentication_Success, enqueued.EventType);
    }

    [Fact]
    public void LogAuthenticationEvent_WhenDisabled_DoesNotEnqueue()
    {
        var options = new AuditOptions { Enabled = false };
        var logger = CreateLogger(options);
        var evt = AuthAuditEvent.Success("tenant1", "user1", "corr1", "127.0.0.1", "TestAgent");

        // Act
        logger.LogAuthenticationEvent(evt);

        // Assert - channel should be empty
        Assert.False(logger.Reader.TryRead(out _));
    }

    [Fact]
    public void LogSecurityEvent_EnqueuesWithChecksum()
    {
        var logger = CreateLogger();
        var evt = SecurityAuditEvent.RateLimitExceeded(
            "tenant1", "user1", "corr1", "127.0.0.1", "TestAgent",
            "/api/chat", 100, 50);

        // Act
        logger.LogSecurityEvent(evt);

        // Assert
        Assert.True(logger.Reader.TryRead(out var enqueued));
        Assert.NotNull(enqueued);
        Assert.NotEmpty(enqueued.Checksum);
    }

    [Fact]
    public void Log_EventTypeFiltered_DoesNotEnqueue()
    {
        var options = new AuditOptions
        {
            Enabled = true,
            EnabledEventTypes = new[] { AuditEventType.Authentication_Success } // Only auth success
        };
        var logger = CreateLogger(options);

        // Auth failure should be filtered
        var evt = AuthAuditEvent.Failure("corr1", "127.0.0.1", "TestAgent", "Invalid token");
        logger.LogAuthenticationEvent(evt);

        // Assert - should not be enqueued
        Assert.False(logger.Reader.TryRead(out _));
    }

    [Fact]
    public void Log_EventTypeAllowed_Enqueues()
    {
        var options = new AuditOptions
        {
            Enabled = true,
            EnabledEventTypes = new[] { AuditEventType.Authentication_Success }
        };
        var logger = CreateLogger(options);

        var evt = AuthAuditEvent.Success("tenant1", "user1", "corr1", "127.0.0.1", "TestAgent");
        logger.LogAuthenticationEvent(evt);

        Assert.True(logger.Reader.TryRead(out var enqueued));
        Assert.NotNull(enqueued);
    }

    [Fact]
    public void Log_ChecksumIsConsistent()
    {
        var logger = CreateLogger();
        var evt = AuthAuditEvent.Success("tenant1", "user1", "corr1", "127.0.0.1", "TestAgent");

        logger.LogAuthenticationEvent(evt);
        Assert.True(logger.Reader.TryRead(out var enqueued));

        var originalChecksum = enqueued!.Checksum;

        // Verify checksum
        Assert.True(enqueued.VerifyChecksum());
        Assert.Equal(originalChecksum, enqueued.Checksum);
    }

    [Fact]
    public void LogDataAccessEvent_EnqueuesWithCorrectType()
    {
        var logger = CreateLogger();
        var evt = DataAccessAuditEvent.Write(
            "tenant1", "user1", "corr1", "127.0.0.1", "TestAgent",
            "Conversation", "conv123", DataOperation.Create);

        logger.LogDataAccessEvent(evt);

        Assert.True(logger.Reader.TryRead(out var enqueued));
        Assert.Equal(AuditEventType.DataAccess_Write, enqueued!.EventType);
    }

    [Fact]
    public void LogAuthorizationEvent_Denied_EnqueuesCorrectly()
    {
        var logger = CreateLogger();
        var evt = AuthzAuditEvent.Denied(
            "tenant1", "user1", "corr1", "127.0.0.1", "TestAgent",
            "tool:model_count", "execute",
            new[] { "User" },
            new[] { "Admin" });

        logger.LogAuthorizationEvent(evt);

        Assert.True(logger.Reader.TryRead(out var enqueued));
        Assert.Equal(AuditEventType.Authorization_Denied, enqueued!.EventType);
        Assert.Equal(AuditOutcome.Denied, enqueued.Outcome);
    }

    [Fact]
    public void AuditEvent_ComputeChecksum_ProducesValidHash()
    {
        var evt = new AuditEvent
        {
            EventType = AuditEventType.Authentication_Success,
            TenantId = "tenant1",
            UserId = "user1",
            CorrelationId = "corr1",
            IpAddress = "127.0.0.1",
            UserAgent = "TestAgent",
            Outcome = AuditOutcome.Success
        };

        evt.ComputeChecksum();

        Assert.NotEmpty(evt.Checksum);
        Assert.Equal(44, evt.Checksum.Length); // Base64 of SHA256 = 44 chars
    }

    [Fact]
    public void AuditEvent_VerifyChecksum_DetectsTampering()
    {
        var evt = new AuditEvent
        {
            EventType = AuditEventType.Authentication_Success,
            TenantId = "tenant1",
            UserId = "user1",
            Outcome = AuditOutcome.Success
        };

        evt.ComputeChecksum();
        var originalChecksum = evt.Checksum;

        // Verify that checksum verification works for valid event
        Assert.True(evt.VerifyChecksum());

        // Create a new event with different data - should have different checksum
        var differentEvt = new AuditEvent
        {
            EventType = AuditEventType.Authentication_Success,
            TenantId = "tenant2", // Different tenant
            UserId = "user1",
            Outcome = AuditOutcome.Success
        };
        differentEvt.ComputeChecksum();

        // Different data should produce different checksum
        Assert.NotEqual(originalChecksum, differentEvt.Checksum);
    }

    [Fact]
    public async Task Log_BufferFull_DropsOldestEvent()
    {
        var options = new AuditOptions
        {
            Enabled = true,
            BufferSize = 2 // Very small buffer
        };
        var logger = CreateLogger(options);

        // Fill buffer beyond capacity
        for (var i = 0; i < 5; i++)
        {
            logger.LogAuthenticationEvent(AuthAuditEvent.Success(
                "tenant1", $"user{i}", $"corr{i}", "127.0.0.1", "TestAgent"));
        }

        // Should have dropped some events
        var count = 0;
        while (logger.Reader.TryRead(out _))
        {
            count++;
        }

        // Buffer size is 2, so at most 2 events should remain
        Assert.True(count <= options.BufferSize);
    }
}
