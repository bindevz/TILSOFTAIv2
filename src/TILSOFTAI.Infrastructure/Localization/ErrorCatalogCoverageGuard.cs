using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Infrastructure.Errors;
using System.Reflection;

namespace TILSOFTAI.Infrastructure.Localization;

/// <summary>
/// Startup guard that verifies the ErrorCatalog has coverage for all critical error codes
/// in all supported languages. Fails fast if translations are missing.
/// </summary>
public sealed class ErrorCatalogCoverageGuard : IHostedService
{
    private readonly IErrorCatalog _errorCatalog;
    private readonly LocalizationOptions _options;
    private readonly ILogger<ErrorCatalogCoverageGuard> _logger;
    private readonly IHostEnvironment _environment;

    // List of critical error codes that MUST have specific translations.
    // We reflect over ErrorCode struct to get all constant values to ensure generic coverage.
    private static readonly Lazy<string[]> RequiredErrorCodes = new(() => 
    {
        return typeof(ErrorCode)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string))
            .Select(fi => (string)fi.GetRawConstantValue()!)
            .ToArray();
    });

    public ErrorCatalogCoverageGuard(
        IErrorCatalog errorCatalog,
        IOptions<LocalizationOptions> options,
        ILogger<ErrorCatalogCoverageGuard> logger,
        IHostEnvironment environment)
    {
        _errorCatalog = errorCatalog ?? throw new ArgumentNullException(nameof(errorCatalog));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Validate LocalizationOptions configuration
        ValidateLocalizationOptions();

        _logger.LogInformation("Verifying Error Catalog coverage for languages: {Languages}", string.Join(", ", _options.SupportedLanguages));

        var missingEntries = new List<string>();

        // Use TryGetExact to detect missing translations (strict check - no fallback allowed)
        foreach (var lang in _options.SupportedLanguages)
        {
            foreach (var code in RequiredErrorCodes.Value)
            {
                if (!_errorCatalog.TryGetExact(code, lang, out _))
                {
                    missingEntries.Add($"Missing exact translation for '{code}' in '{lang}'");
                }
            }
        }

        if (missingEntries.Count > 0)
        {
            var errorMsg = $"Error Catalog is missing required translations: {string.Join("; ", missingEntries)}";
            _logger.LogCritical(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        _logger.LogInformation("Error Catalog coverage verified successfully.");
        return Task.CompletedTask;
    }

    private void ValidateLocalizationOptions()
    {
        // Validate SupportedLanguages is not empty
        if (_options.SupportedLanguages == null || _options.SupportedLanguages.Length == 0)
        {
            var errorMsg = "LocalizationOptions.SupportedLanguages must not be empty.";
            _logger.LogCritical(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        // In Production, 'en' must be included in SupportedLanguages
        if (_environment.IsProduction() && !_options.SupportedLanguages.Contains("en", StringComparer.OrdinalIgnoreCase))
        {
            var errorMsg = "LocalizationOptions.SupportedLanguages must include 'en' in Production environment.";
            _logger.LogCritical(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
