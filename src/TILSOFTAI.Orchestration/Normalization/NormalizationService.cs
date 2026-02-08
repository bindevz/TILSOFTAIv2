using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
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

    // PATCH 33.02: Patterns that indicate a rule strips whitespace
    private static readonly string[] UnsafeWhitespacePatterns = new[]
    {
        @"\s", @"\p{Z", @"[\s", @"\t", @"\r", @"\n"
    };

    private readonly INormalizationRuleProvider _ruleProvider;
    private readonly IRedisCacheProvider _cacheProvider;
    private readonly RedisOptions _redisOptions;
    private readonly TimeSpan _defaultTtl;
    private readonly SeasonNormalizer _seasonNormalizer;
    private readonly ConcurrentDictionary<string, CacheEntry> _memoryCache = new();
    private readonly ILogger<NormalizationService> _logger;

    public NormalizationService(
        INormalizationRuleProvider ruleProvider,
        IRedisCacheProvider cacheProvider,
        IOptions<RedisOptions> redisOptions,
        ILogger<NormalizationService> logger)
    {
        _ruleProvider = ruleProvider ?? throw new ArgumentNullException(nameof(ruleProvider));
        _cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));
        _redisOptions = redisOptions?.Value ?? throw new ArgumentNullException(nameof(redisOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        // PATCH 29.05: Pre-canonicalize whitespace for deterministic processing
        var output = PromptTextCanonicalizer.Canonicalize(input);
        if (string.IsNullOrWhiteSpace(output))
        {
            return string.Empty;
        }

        var tenantId = string.IsNullOrWhiteSpace(context.TenantId) ? "unknown" : context.TenantId;
        var rules = await GetRulesAsync(tenantId, ct);

        // PATCH 33.02: Count whitespace-separated tokens before normalization
        var inputTokenCount = TokenCount(output);

        foreach (var rule in rules.OrderBy(rule => rule.Priority).ThenBy(rule => rule.RuleKey, StringComparer.OrdinalIgnoreCase))
        {
            // PATCH 33.02: Skip unsafe whitespace-stripping rules
            if (IsUnsafeWhitespaceRule(rule))
            {
                _logger.LogWarning(
                    "SkippingUnsafeNormalizationRule | RuleKey: {RuleKey} | Pattern: {Pattern} | " +
                    "Replacement is empty and pattern matches whitespace characters",
                    rule.RuleKey, rule.Pattern);
                continue;
            }

            output = ApplyRule(output, rule);
        }

        output = _seasonNormalizer.ExpandMarkedSeasons(output);
        
        // PATCH 29.05: Post-canonicalize and guard against empty result
        output = PromptTextCanonicalizer.Canonicalize(output);
        if (string.IsNullOrWhiteSpace(output))
        {
            // Guard: if normalization stripped all content, return original (trimmed)
            return input.Trim();
        }

        // PATCH 33.02: Post-guard — if normalization collapsed tokens, revert
        var outputTokenCount = TokenCount(output);
        if (inputTokenCount >= 2 && outputTokenCount <= 1)
        {
            _logger.LogWarning(
                "NormalizationCollapsedTokens | InputTokens: {InputTokens} | OutputTokens: {OutputTokens} | " +
                "Reverting to pre-normalization input",
                inputTokenCount, outputTokenCount);
            return PromptTextCanonicalizer.Canonicalize(input) ?? input.Trim();
        }
        
        return output;
    }

    /// <summary>
    /// PATCH 33.02: Detect rules that strip whitespace globally.
    /// Unsafe if: Replacement is null/empty AND Pattern contains any whitespace-matching regex tokens.
    /// </summary>
    public static bool IsUnsafeWhitespaceRule(NormalizationRuleRecord rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Pattern))
            return false;

        // Only dangerous if replacement is empty (strips matched content)
        if (!string.IsNullOrEmpty(rule.Replacement))
            return false;

        foreach (var marker in UnsafeWhitespacePatterns)
        {
            if (rule.Pattern.Contains(marker, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Count whitespace-separated tokens in a string.
    /// </summary>
    public static int TokenCount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
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
