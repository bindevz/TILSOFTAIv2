using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Infrastructure.Resilience;
using Xunit;

namespace TILSOFTAI.Tests.Integration.Resilience;

public class RetryIntegrationTests
{
    [Fact]
    public async Task RetryDelegatingHandler_ShouldRetry_On429()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        int calls = 0;
        
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(() =>
            {
                calls++;
                if (calls < 3)
                {
                    return new HttpResponseMessage((HttpStatusCode)429) { Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(10)) } };
                }
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var options = new RetryOptions { MaxRetries = 3, InitialDelay = TimeSpan.Zero };
        var loggerFactory = new Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory();
        var metrics = new Mock<TILSOFTAI.Domain.Metrics.IMetricsService>();
        var policy = new PollyRetryPolicy("test", options, loggerFactory.CreateLogger("test"), metrics.Object);

        var retryHandler = new RetryDelegatingHandler(policy)
        {
            InnerHandler = handlerMock.Object
        };

        var client = new HttpClient(retryHandler);
        var response = await client.GetAsync("http://localhost");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, calls); // 1 initial + 2 retries
    }
}
