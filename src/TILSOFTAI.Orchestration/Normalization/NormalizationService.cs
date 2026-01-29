using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Caching;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Normalization;

public sealed class NormalizationService : INormalizationService
{
    private static readonly JsonSerializerOptions CacheSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly INormalizationRuleProvider _ruleProvider;
    private readonly IRedisCacheProvider _cacheProvider;
    private readonly RedisOptions _redisOptions;
    private readonly TimeSpan _defaultTtl;
    private readonly SeasonNormalizer _seasonNormalizer;
    private readonly ConcurrentDictionary<string, CacheEntry> _memoryCache = new();

    public NormalizationService(
        INormalizationRuleProvider ruleProvider,
        IRedisCacheProvider cacheProvider,
        IOptions<RedisOptions> redisOptions)
    {
        _ruleProvider = ruleProvider ?? throw new ArgumentNullException(nameof(ruleProvider));
        _cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));
        _redisOptions = redisOptions?.Value ?? throw new ArgumentNullException(nameof(redisOptions));
        _seasonNormalizer = new SeasonNormalizer();

        var ttlMinutes = Math.Max(30, _redisOptions.DefaultTtlMinutes);
        _defaultTtl = TimeSpan.FromMinutes(ttlMinutes);
    }

    public async Task<string> NormalizeAsync(string input, TilsoftExecutionContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var tenantId = string.IsNullOrWhiteSpace(context.TenantId) ? "unknown" : context.TenantId;
        var rules = await GetRulesAsync(tenantId, ct);

        var output = input.Trim();
        foreach (var rule in rules.OrderBy(rule => rule.Priority).ThenBy(rule => rule.RuleKey, StringComparer.OrdinalIgnoreCase))
        {
            output = ApplyRule(output, rule);
        }

        output = _seasonNormalizer.ExpandMarkedSeasons(output);
        return output.Trim();
    }

    private async Task<IReadOnlyList<NormalizationRuleRecord>> GetRulesAsync(string tenantId, CancellationToken ct)
    {
        var cacheKey = $"normrules:{tenantId}";

        if (_redisOptions.Enabled)
        {
            var cached = await _cacheProvider.GetStringAsync(cacheKey, ct);
            if (!string.IsNullOrWhiteSpace(cached))
            {
                var rules = JsonSerializer.Deserialize<List<NormalizationRuleRecord>>(cached, CacheSerializerOptions);
                if (rules is not null)
                {
                    return rules;
                }
            }
        }
        else if (TryGetFromMemory(cacheKey, out var memoryRules))
        {
            return memoryRules;
        }

        var loaded = await _ruleProvider.GetRulesAsync(tenantId, ct);
        var ordered = loaded
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.RuleKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (_redisOptions.Enabled)
        {
            var serialized = JsonSerializer.Serialize(ordered, CacheSerializerOptions);
            await _cacheProvider.SetStringAsync(cacheKey, serialized, _defaultTtl, ct);
        }
        else
        {
            _memoryCache[cacheKey] = new CacheEntry(ordered, DateTimeOffset.UtcNow.Add(_defaultTtl));
        }

        return ordered;
    }

    private static string ApplyRule(string input, NormalizationRuleRecord rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Pattern))
        {
            return input;
        }

        if (string.Equals(rule.Replacement, SeasonNormalizer.Marker, StringComparison.Ordinal))
        {
            return Regex.Replace(
                input,
                rule.Pattern,
                match =>
                {
                    if (match.Groups.Count < 3)
                    {
                        return match.Value;
                    }

                    var first = match.Groups[1].Value;
                    var second = match.Groups[2].Value;
                    if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
                    {
                        return match.Value;
                    }

                    return SeasonNormalizer.CreateMarker(first, second);
                },
                RegexOptions.CultureInvariant);
        }

        return Regex.Replace(input, rule.Pattern, rule.Replacement, RegexOptions.CultureInvariant);
    }

    private bool TryGetFromMemory(string cacheKey, out IReadOnlyList<NormalizationRuleRecord> rules)
    {
        rules = Array.Empty<NormalizationRuleRecord>();
        if (!_memoryCache.TryGetValue(cacheKey, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _memoryCache.TryRemove(cacheKey, out _);
            return false;
        }

        rules = entry.Rules;
        return true;
    }

    private sealed record CacheEntry(IReadOnlyList<NormalizationRuleRecord> Rules, DateTimeOffset ExpiresAt);
}
