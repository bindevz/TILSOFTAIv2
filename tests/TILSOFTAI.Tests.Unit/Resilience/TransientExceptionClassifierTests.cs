using System;
using System.Net;
using System.Net.Http;
using Microsoft.Data.SqlClient;
using TILSOFTAI.Infrastructure.Resilience;
using Xunit;

namespace TILSOFTAI.Tests.Unit.Resilience;

public class TransientExceptionClassifierTests
{
    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public void IsTransient_ShouldReturnTrue_ForTransientHttpStatusCodes(int statusCode)
    {
        var ex = new HttpRequestException("Error", null, (HttpStatusCode)statusCode);
        Assert.True(TransientExceptionClassifier.IsTransient(ex));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    public void IsTransient_ShouldReturnFalse_ForNonTransientHttpStatusCodes(int statusCode)
    {
        var ex = new HttpRequestException("Error", null, (HttpStatusCode)statusCode);
        Assert.False(TransientExceptionClassifier.IsTransient(ex));
    }

    [Fact]
    public void IsTransient_ShouldReturnTrue_ForTimeoutException()
    {
        Assert.True(TransientExceptionClassifier.IsTransient(new TimeoutException()));
    }
}
