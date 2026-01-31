using System.Text;
using System.Text.Json;
using TILSOFTAI.Tests.Contract.Fixtures;

namespace TILSOFTAI.Tests.Contract.Streaming;

/// <summary>
/// Contract tests for SSE (Server-Sent Events) error envelope format.
/// Verifies that streaming errors return proper error envelopes without raw internals leak.
/// </summary>
public class SseErrorEnvelopeTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SseErrorEnvelopeTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task StreamEndpoint_TriggeredError_ReturnsErrorEnvelopeInSSE()
    {
        // Arrange: Create request with test-only error trigger header
        var client = _factory.CreateClient();
        
        var requestBody = new
        {
            input = "Test input"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat/stream");
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody), 
            Encoding.UTF8, 
            "application/json");
        
        // Add test headers for authentication and error trigger
        request.Headers.Add("X-Test-Tenant", "TENANT_SSE");
        request.Headers.Add("X-Test-User", "USER_SSE");
        request.Headers.Add("X-Test-Roles", "Admin");
        request.Headers.Add("X-Test-Trigger-Error", "true");

        // Act: Send request and read SSE response
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        var content = await response.Content.ReadAsStringAsync();

        // Assert: Response should contain SSE error event with proper envelope
        Assert.Contains("event:", content);
        Assert.Contains("data:", content);

        // Parse SSE events
        var events = ParseSseEvents(content);
        Assert.NotEmpty(events);

        // Find error event
        var errorEvent = events.FirstOrDefault(e => e.EventType?.Contains("error", StringComparison.OrdinalIgnoreCase) == true);
        Assert.NotNull(errorEvent);

        // Parse error envelope JSON
        var errorEnvelope = JsonDocument.Parse(errorEvent.Data);
        var root = errorEnvelope.RootElement;

        // Verify required error envelope fields
        Assert.True(root.TryGetProperty("code", out var code), "Error envelope must have 'code' field");
        Assert.True(root.TryGetProperty("messageKey", out _), "Error envelope must have 'messageKey' field");
        Assert.True(root.TryGetProperty("correlationId", out var correlationId), "Error envelope must have 'correlationId' field");
        Assert.True(root.TryGetProperty("traceId", out var traceId), "Error envelope must have 'traceId' field");

        // Verify observability fields are not empty
        Assert.False(string.IsNullOrWhiteSpace(correlationId.GetString()), "correlationId should not be empty");
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()), "traceId should not be empty");

        // Verify expected error code
        Assert.Equal("CHAT_FAILED", code.GetString());
    }

    [Fact]
    public async Task StreamEndpoint_TriggeredError_NoRawInternalsLeak()
    {
        // Arrange: Create request with test-only error trigger header
        var client = _factory.CreateClient();
        
        var requestBody = new
        {
            input = "Test input for internals check"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat/stream");
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody), 
            Encoding.UTF8, 
            "application/json");
        
        request.Headers.Add("X-Test-Tenant", "TENANT_INTERNAL");
        request.Headers.Add("X-Test-User", "USER_INTERNAL");
        request.Headers.Add("X-Test-Roles", "User");
        request.Headers.Add("X-Test-Trigger-Error", "true");

        // Act: Send request and read SSE response
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        var content = await response.Content.ReadAsStringAsync();

        // Assert: No raw .NET exception details should leak
        Assert.DoesNotContain("System.", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", content); // Stack trace line pattern
        Assert.DoesNotContain("InnerException", content, StringComparison.OrdinalIgnoreCase);
        
        // Verify only structured error envelope is present
        var events = ParseSseEvents(content);
        var errorEvent = events.FirstOrDefault(e => e.EventType?.Contains("error", StringComparison.OrdinalIgnoreCase) == true);
        
        Assert.NotNull(errorEvent);
        
        // The error data should be valid JSON (not raw exception text)
        var exception = Record.Exception(() => JsonDocument.Parse(errorEvent.Data));
        Assert.Null(exception); // Should parse without error
    }

    [Fact]
    public async Task StreamEndpoint_TriggeredError_HasRequestIdInEnvelope()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        var requestBody = new { input = "Test for requestId" };
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat/stream");
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody), 
            Encoding.UTF8, 
            "application/json");
        
        request.Headers.Add("X-Test-Tenant", "TENANT_REQ");
        request.Headers.Add("X-Test-User", "USER_REQ");
        request.Headers.Add("X-Test-Roles", "Admin");
        request.Headers.Add("X-Test-Trigger-Error", "true");

        // Act
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        var content = await response.Content.ReadAsStringAsync();

        // Assert: Error envelope should include requestId for tracing
        var events = ParseSseEvents(content);
        var errorEvent = events.FirstOrDefault(e => e.EventType?.Contains("error", StringComparison.OrdinalIgnoreCase) == true);
        
        Assert.NotNull(errorEvent);
        
        var errorEnvelope = JsonDocument.Parse(errorEvent.Data);
        var root = errorEnvelope.RootElement;
        
        Assert.True(root.TryGetProperty("requestId", out var requestId), "Error envelope should have 'requestId' field");
        Assert.False(string.IsNullOrWhiteSpace(requestId.GetString()), "requestId should not be empty");
    }

    /// <summary>
    /// Parse SSE (Server-Sent Events) format into structured events.
    /// </summary>
    private List<SseEvent> ParseSseEvents(string sseContent)
    {
        var events = new List<SseEvent>();
        var lines = sseContent.Split('\n');
        
        SseEvent? currentEvent = null;
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim('\r');
            
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                // Empty line indicates end of event
                if (currentEvent != null)
                {
                    events.Add(currentEvent);
                    currentEvent = null;
                }
                continue;
            }
            
            if (trimmedLine.StartsWith("event:", StringComparison.Ordinal))
            {
                currentEvent ??= new SseEvent();
                currentEvent.EventType = trimmedLine.Substring(6).Trim();
            }
            else if (trimmedLine.StartsWith("data:", StringComparison.Ordinal))
            {
                currentEvent ??= new SseEvent();
                var dataContent = trimmedLine.Substring(5).Trim();
                currentEvent.Data = string.IsNullOrEmpty(currentEvent.Data) 
                    ? dataContent 
                    : currentEvent.Data + "\n" + dataContent;
            }
        }
        
        // Add last event if exists
        if (currentEvent != null)
        {
            events.Add(currentEvent);
        }
        
        return events;
    }

    private class SseEvent
    {
        public string? EventType { get; set; }
        public string Data { get; set; } = string.Empty;
    }
}
