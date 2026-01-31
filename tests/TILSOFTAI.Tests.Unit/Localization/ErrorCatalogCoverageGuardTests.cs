using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Infrastructure.Localization;

namespace TILSOFTAI.Tests.Unit.Localization;

public class ErrorCatalogCoverageGuardTests
{
    private readonly Mock<IErrorCatalog> _catalogMock;
    private readonly Mock<ILogger<ErrorCatalogCoverageGuard>> _loggerMock;
    private readonly LocalizationOptions _options;

    public ErrorCatalogCoverageGuardTests()
    {
        _catalogMock = new Mock<IErrorCatalog>();
        _loggerMock = new Mock<ILogger<ErrorCatalogCoverageGuard>>();
        _options = new LocalizationOptions
        {
            DefaultLanguage = "en",
            SupportedLanguages = new[] { "en" }
        };
    }

    [Fact]
    public async Task StartAsync_WhenAllCodesPresent_Succeeds()
    {
        // Arrange
        // Mock the catalog to return a definition with the same code (simulating found)
        // For verify purpose, we just need it to return definition.Code == requestedCode
        _catalogMock.Setup(c => c.Get(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string code, string lang) => new ErrorDefinition(code, lang, "Test Message"));

        var guard = new ErrorCatalogCoverageGuard(
            _catalogMock.Object,
            Options.Create(_options),
            _loggerMock.Object);

        // Act & Assert
        await guard.StartAsync(CancellationToken.None);
        
        // No exception thrown
    }

    [Fact]
    public async Task StartAsync_WhenCodeMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        // Simulate missing code by returning UnhandledError fallback
        _catalogMock.Setup(c => c.Get(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string code, string lang) => 
            {
                if (code == ErrorCode.RequestTooLarge)
                {
                    // Return fallback for this specific code to simulate missing entry
                    return new ErrorDefinition(ErrorCode.UnhandledError, lang, "Fallback");
                }
                return new ErrorDefinition(code, lang, "Test Message");
            });

        var guard = new ErrorCatalogCoverageGuard(
            _catalogMock.Object,
            Options.Create(_options),
            _loggerMock.Object);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => guard.StartAsync(CancellationToken.None));
        Assert.Contains("missing translations", ex.Message);
        Assert.Contains(ErrorCode.RequestTooLarge, ex.Message);
    }
}
