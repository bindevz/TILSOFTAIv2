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
        ILogger<ErrorCatalogCoverageGuard> logger)
    {
        _errorCatalog = errorCatalog ?? throw new ArgumentNullException(nameof(errorCatalog));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Verifying Error Catalog coverage for languages: {Languages}", string.Join(", ", _options.SupportedLanguages));

        var missingEntries = new List<string>();

        foreach (var lang in _options.SupportedLanguages)
        {
            foreach (var code in RequiredErrorCodes.Value)
            {
                var definition = _errorCatalog.Get(code, lang);
                
                // If the returned code doesn't match requested code, it means we hit fallback to UnhandledError
                // Or if message matches fallback/default exactly (imperfect check, but catches generic fallback)
                if (definition.Code != code)
                {
                    missingEntries.Add($"Missing '{code}' in '{lang}'");
                }
            }
        }

        if (missingEntries.Count > 0)
        {
            var errorMsg = $"Error Catalog is missing translations for enabled languages: {string.Join("; ", missingEntries)}";
            _logger.LogCritical(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        _logger.LogInformation("Error Catalog coverage verified successfully.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
