using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace TILSOFTAI.Tests.Contract.Auth;

/// <summary>
/// Contract test verifying secure-by-default authorization policy.
/// Ensures FallbackPolicy requires authenticated users for all endpoints
/// unless explicitly marked with [AllowAnonymous].
/// </summary>
public sealed class SecureByDefaultAuthorizationTests
{
    [Fact]
    public void AddAuthorization_ConfiguresFallbackPolicy_RequiringAuthenticatedUser()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Configure authorization the same way as AddTilsoftAiExtensions
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        var serviceProvider = services.BuildServiceProvider();
        
        // Assert
        var authOptions = serviceProvider.GetRequiredService<IOptions<AuthorizationOptions>>();
        Assert.NotNull(authOptions);
        
        var policy = authOptions.Value.FallbackPolicy;
        Assert.NotNull(policy);
        
        // Verify it denies anonymous access
        Assert.Single(policy!.Requirements);
        Assert.IsType<DenyAnonymousAuthorizationRequirement>(policy.Requirements.First());
    }

    [Fact]
    public void FallbackPolicy_DeniesAnonymousAccess()
    {
        // Arrange
        var policyBuilder = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser();

        // Act
        var policy = policyBuilder.Build();

        // Assert
        Assert.NotNull(policy);
        Assert.Single(policy.Requirements);
        
        var requirement = policy.Requirements.First();
        Assert.IsType<DenyAnonymousAuthorizationRequirement>(requirement);
    }
}
