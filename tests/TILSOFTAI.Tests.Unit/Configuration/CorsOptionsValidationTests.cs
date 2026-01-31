using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Tests.Unit.Configuration;

public class CorsOptionsValidationTests
{
    [Fact]
    public void ValidateOnStart_WhenEnabledWithoutOrigins_ThrowsOptionsValidationException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:Enabled"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<CorsOptions>()
            .Bind(config.GetSection("Cors"))
            .Validate(options =>
            {
                if (!options.Enabled) return true;
                return options.AllowedOrigins != null && options.AllowedOrigins.Length > 0;
            }, "CORS: Enabled=true requires at least one allowed origin. Update Cors:AllowedOrigins in configuration.")
            .ValidateOnStart();

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Act & Assert
        var ex = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<CorsOptions>>().Value);

        Assert.Contains("requires at least one allowed origin", ex.Message);
    }

    [Fact]
    public void ValidateOnStart_WhenDisabledWithoutOrigins_Succeeds()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:Enabled"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<CorsOptions>()
            .Bind(config.GetSection("Cors"))
            .Validate(options =>
            {
                if (!options.Enabled) return true;
                return options.AllowedOrigins != null && options.AllowedOrigins.Length > 0;
            }, "CORS: Enabled=true requires at least one allowed origin. Update Cors:AllowedOrigins in configuration.")
            .ValidateOnStart();

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var corsOptions = serviceProvider.GetRequiredService<IOptions<CorsOptions>>().Value;

        // Assert
        Assert.False(corsOptions.Enabled);
    }

    [Fact]
    public void ValidateOnStart_WhenEnabledWithOrigins_Succeeds()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:Enabled"] = "true",
                ["Cors:AllowedOrigins:0"] = "https://example.com"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<CorsOptions>()
            .Bind(config.GetSection("Cors"))
            .Validate(options =>
            {
                if (!options.Enabled) return true;
                return options.AllowedOrigins != null && options.AllowedOrigins.Length > 0;
            }, "CORS: Enabled=true requires at least one allowed origin. Update Cors:AllowedOrigins in configuration.")
            .ValidateOnStart();

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var corsOptions = serviceProvider.GetRequiredService<IOptions<CorsOptions>>().Value;

        // Assert
        Assert.True(corsOptions.Enabled);
        Assert.Single(corsOptions.AllowedOrigins);
        Assert.Equal("https://example.com", corsOptions.AllowedOrigins[0]);
    }

    [Fact]
    public void Normalize_RemovesTrailingSlashes()
    {
        // Arrange
        var options = new CorsOptions
        {
            AllowedOrigins = new[]
            {
                "https://example.com/",
                "https://api.example.com/",
                "https://notrailingslash.com"
            }
        };

        // Act
        options.Normalize();

        // Assert
        Assert.Equal("https://example.com", options.AllowedOrigins[0]);
        Assert.Equal("https://api.example.com", options.AllowedOrigins[1]);
        Assert.Equal("https://notrailingslash.com", options.AllowedOrigins[2]);
    }

    [Fact]
    public void Normalize_TrimsWhitespace()
    {
        // Arrange
        var options = new CorsOptions
        {
            AllowedOrigins = new[]
            {
                "  https://example.com  ",
                "\thttps://api.example.com\t",
                " https://test.com/ "
            }
        };

        // Act
        options.Normalize();

        // Assert
        Assert.Equal("https://example.com", options.AllowedOrigins[0]);
        Assert.Equal("https://api.example.com", options.AllowedOrigins[1]);
        Assert.Equal("https://test.com", options.AllowedOrigins[2]);
    }

    [Fact]
    public void Normalize_PreservesWildcard()
    {
        // Arrange
        var options = new CorsOptions
        {
            AllowedOrigins = new[] { " * " }
        };

        // Act
        options.Normalize();

        // Assert
        Assert.Equal("*", options.AllowedOrigins[0]);
    }

    [Fact]
    public void ValidateOnStart_WhenCredentialsWithWildcard_ThrowsOptionsValidationException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:Enabled"] = "true",
                ["Cors:AllowCredentials"] = "true",
                ["Cors:AllowedOrigins:0"] = "*"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<CorsOptions>()
            .Bind(config.GetSection("Cors"))
            .Validate(options =>
            {
                if (!options.Enabled) return true;
                return options.AllowedOrigins != null && options.AllowedOrigins.Length > 0;
            }, "CORS: Enabled=true requires at least one allowed origin.")
            .Validate(options =>
            {
                if (!options.Enabled) return true;
                if (options.AllowCredentials && options.AllowedOrigins.Any(o => o == "*"))
                    return false;
                return true;
            }, "CORS: AllowCredentials=true requires explicit origins, not wildcard '*'. Update Cors:AllowedOrigins in configuration.")
            .ValidateOnStart();

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Act & Assert
        var ex = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<CorsOptions>>().Value);

        Assert.Contains("requires explicit origins", ex.Message);
    }

    [Fact]
    public void ValidateOnStart_WhenEnabledWithEmptyArray_ThrowsOptionsValidationException()
    {
        // Arrange
        var options = new CorsOptions
        {
            Enabled = true,
            AllowedOrigins = Array.Empty<string>()
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:Enabled"] = "true",
                ["Cors:AllowedOrigins"] = ""
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<CorsOptions>()
            .Bind(config.GetSection("Cors"))
            .Validate(opt =>
            {
                if (!opt.Enabled) return true;
                return opt.AllowedOrigins != null && opt.AllowedOrigins.Length > 0;
            }, "CORS: Enabled=true requires at least one allowed origin. Update Cors:AllowedOrigins in configuration.")
            .ValidateOnStart();

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Act & Assert
        var ex = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<CorsOptions>>().Value);

        Assert.Contains("requires at least one allowed origin", ex.Message);
    }
}
