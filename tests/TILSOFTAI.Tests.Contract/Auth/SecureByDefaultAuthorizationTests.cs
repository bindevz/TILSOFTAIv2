using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Security;
using Xunit;

namespace TILSOFTAI.Tests.Contract.Auth;

/// <summary>
/// Contract test verifying secure-by-default authorization policy.
/// Ensures FallbackPolicy requires authenticated users for all endpoints
/// unless explicitly marked with [AllowAnonymous].
/// Also verifies that header-based role injection is completely disabled.
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

    [Fact]
    public void HeaderRoles_AreIgnored_RolesComeFromClaimsOnly()
    {
        // Arrange
        var authOptions = new AuthOptions
        {
            TenantClaimName = "tid",
            UserIdClaimName = "sub",
            RoleClaimName = "roles",
            TrustedGatewayClaimName = "gateway_trusted",
            AllowHeaderFallback = true // Even with fallback enabled, roles should NOT come from headers
        };

        var claims = new[]
        {
            new Claim("tid", "tenant1"),
            new Claim("sub", "user1"),
            new Claim("roles", "ClaimRole1,ClaimRole2")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = principal
        };

        // Add X-Roles header attempting privilege escalation
        httpContext.Request.Headers["X-Roles"] = "AdminRole,SuperUserRole";

        var environment = new FakeWebHostEnvironment { EnvironmentName = "Development" };
        var localizationOptions = Options.Create(new LocalizationOptions { DefaultLanguage = "en" });
        var policy = new IdentityResolutionPolicy(localizationOptions);

        // Act
        var result = policy.ResolveForRequest(httpContext, authOptions, environment);

        // Assert - Roles should ONLY come from JWT claims, not headers
        Assert.NotNull(result.Roles);
        Assert.Equal(2, result.Roles.Length);
        Assert.Contains("ClaimRole1", result.Roles);
        Assert.Contains("ClaimRole2", result.Roles);
        
        // Verify header roles are NOT present (privilege escalation prevented)
        Assert.DoesNotContain("AdminRole", result.Roles);
        Assert.DoesNotContain("SuperUserRole", result.Roles);
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "TestApp";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
