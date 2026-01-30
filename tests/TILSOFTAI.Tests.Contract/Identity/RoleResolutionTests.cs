using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Security;
using Xunit;

namespace TILSOFTAI.Tests.Contract.Identity;

public sealed class RoleResolutionTests
{
    [Fact]
    public void ResolveForRequest_IgnoresHeaderRoles_WhenClaimsPresent()
    {
        var authOptions = new AuthOptions
        {
            RoleClaimName = "roles",
            AllowHeaderFallback = true
        };
        var policy = new IdentityResolutionPolicy(Options.Create(new LocalizationOptions
        {
            DefaultLanguage = "en"
        }));

        var context = BuildContext(
            rolesClaim: "admin,ops",
            headerRoles: "superuser");

        var result = policy.ResolveForRequest(context, authOptions, new TestEnvironment());

        Assert.Contains("admin", result.Roles);
        Assert.Contains("ops", result.Roles);
        Assert.DoesNotContain("superuser", result.Roles);
    }

    [Fact]
    public void ResolveForRequest_ReturnsEmptyRoles_WhenOnlyHeaderRolesPresent()
    {
        var authOptions = new AuthOptions
        {
            RoleClaimName = "roles",
            AllowHeaderFallback = true
        };
        var policy = new IdentityResolutionPolicy(Options.Create(new LocalizationOptions
        {
            DefaultLanguage = "en"
        }));

        var context = BuildContext(
            rolesClaim: null,
            headerRoles: "admin");

        var result = policy.ResolveForRequest(context, authOptions, new TestEnvironment());

        Assert.Empty(result.Roles);
    }

    private static DefaultHttpContext BuildContext(string? rolesClaim, string? headerRoles)
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity("Test");

        if (!string.IsNullOrWhiteSpace(rolesClaim))
        {
            identity.AddClaim(new Claim("roles", rolesClaim));
        }

        context.User = new ClaimsPrincipal(identity);

        if (!string.IsNullOrWhiteSpace(headerRoles))
        {
            context.Request.Headers["X-Roles"] = headerRoles;
        }

        return context;
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
