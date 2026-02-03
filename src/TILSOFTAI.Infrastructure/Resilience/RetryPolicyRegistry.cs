using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Domain.Resilience;

namespace TILSOFTAI.Infrastructure.Resilience;

public class RetryPolicyRegistry
{
    private readonly ConcurrentDictionary<string, TILSOFTAI.Domain.Resilience.IRetryPolicy> _policies = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly IMetricsService _metrics;
    private readonly ResilienceOptions _options;

    public RetryPolicyRegistry(
        ILoggerFactory loggerFactory,
        IMetricsService metrics,
        IOptions<ResilienceOptions> options)
    {
        _loggerFactory = loggerFactory;
        _metrics = metrics;
        _options = options.Value;
    }

    public TILSOFTAI.Domain.Resilience.IRetryPolicy GetOrCreate(string name, RetryOptions? optionsOverride = null)
    {
        return _policies.GetOrAdd(name, _ =>
        {
            var options = optionsOverride ?? GetOptionsFor(name);
            var logger = _loggerFactory.CreateLogger<PollyRetryPolicy>();
            return new PollyRetryPolicy(name, options, logger, _metrics);
        });
    }

    private RetryOptions GetOptionsFor(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "llm" => _options.LlmRetry,
            "sql" => _options.SqlRetry,
            "redis" => _options.RedisRetry,
            _ => new RetryOptions()
        };
    }
}
