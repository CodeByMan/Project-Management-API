using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;
using ProjectManager.Configuration;
using ProjectManager.Models;
using System.IdentityModel.Tokens.Jwt;

namespace ProjectManager.Tests;

public class JwtTests
{
    [Fact]
    public void ValidationParameters_RequireIssuerAudienceSignatureAndLifetime()
    {
        var settings = JwtSettings.Load(CreateConfiguration());
        var parameters = settings.CreateValidationParameters();

        Assert.True(parameters.ValidateIssuerSigningKey);
        Assert.True(parameters.ValidateIssuer);
        Assert.Equal("ProjectManager.Api.Tests", parameters.ValidIssuer);
        Assert.True(parameters.ValidateAudience);
        Assert.Equal("ProjectManager.Client.Tests", parameters.ValidAudience);
        Assert.True(parameters.ValidateLifetime);
        Assert.True(parameters.RequireExpirationTime);
        Assert.Equal(TimeSpan.Zero, parameters.ClockSkew);
    }

    [Fact]
    public void MissingAudience_IsRejected()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "unit-test-key-that-is-long-enough-for-hmac-sha512-and-more-than-sixty-four-bytes-2026",
                ["Jwt:Issuer"] = "ProjectManager.Api.Tests",
                ["Jwt:AccessTokenMinutes"] = "30"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => JwtSettings.Load(configuration));
    }

    [Fact]
    public async Task TokenService_EmitsConfiguredIssuerAudienceRolesAndShortExpiry()
    {
        var user = new ApplicationUser
        {
            Id = "user-1",
            Email = "user@example.com",
            UserName = "user@example.com"
        };
        var userManager = CreateUserManager();
        userManager.Setup(manager => manager.GetSecurityStampAsync(user)).ReturnsAsync("stamp-1");
        userManager.Setup(manager => manager.GetRolesAsync(user)).ReturnsAsync(new[] { "Member" });
        userManager.Setup(manager => manager.GetClaimsAsync(user)).ReturnsAsync(Array.Empty<System.Security.Claims.Claim>());
        var roleManager = CreateRoleManager();
        roleManager.Setup(manager => manager.FindByNameAsync("Member")).ReturnsAsync((IdentityRole?)null);

        var service = new TokenService(CreateConfiguration(), userManager.Object, roleManager.Object);
        var tokenString = await service.CreateToken(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenString);

        Assert.Equal("ProjectManager.Api.Tests", token.Issuer);
        Assert.Contains("ProjectManager.Client.Tests", token.Audiences);
        var nameIdentifierClaimType =
            JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap[System.Security.Claims.ClaimTypes.NameIdentifier];

        var roleClaimType =
            JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap[System.Security.Claims.ClaimTypes.Role];

        Assert.Contains(token.Claims, claim =>
            claim.Type == nameIdentifierClaimType && claim.Value == user.Id);

        Assert.Contains(token.Claims, claim =>
            claim.Type == roleClaimType && claim.Value == "Member");
        Assert.Contains(token.Claims, claim => claim.Type == ProjectManager.Services.JwtSecurityStampValidator.SecurityStampClaimType && claim.Value == "stamp-1");
        Assert.InRange(token.ValidTo - token.ValidFrom, TimeSpan.FromMinutes(29), TimeSpan.FromMinutes(31));
    }

    [Fact]
    public async Task SecurityStampValidator_RejectsTokenAfterSecurityStampChanges()
    {
        var user = new ApplicationUser { Id = "user-1", SecurityStamp = "new-stamp" };
        var userManager = CreateUserManager();
        userManager.Setup(manager => manager.FindByIdAsync(user.Id)).ReturnsAsync(user);
        userManager.Setup(manager => manager.GetSecurityStampAsync(user)).ReturnsAsync("new-stamp");
        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id),
                new System.Security.Claims.Claim(ProjectManager.Services.JwtSecurityStampValidator.SecurityStampClaimType, "old-stamp")
            }, "Test"));

        var validator = new ProjectManager.Services.JwtSecurityStampValidator(userManager.Object);

        Assert.False(await validator.IsCurrentAsync(principal));
    }

    [Fact]
    public async Task SecurityStampValidator_AcceptsCurrentSecurityStamp()
    {
        var user = new ApplicationUser { Id = "user-1", SecurityStamp = "stamp-1" };
        var userManager = CreateUserManager();
        userManager.Setup(manager => manager.FindByIdAsync(user.Id)).ReturnsAsync(user);
        userManager.Setup(manager => manager.GetSecurityStampAsync(user)).ReturnsAsync("stamp-1");
        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id),
                new System.Security.Claims.Claim(ProjectManager.Services.JwtSecurityStampValidator.SecurityStampClaimType, "stamp-1")
            }, "Test"));

        var validator = new ProjectManager.Services.JwtSecurityStampValidator(userManager.Object);

        Assert.True(await validator.IsCurrentAsync(principal));
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "unit-test-key-that-is-long-enough-for-hmac-sha512-and-more-than-sixty-four-bytes-2026",
                ["Jwt:Issuer"] = "ProjectManager.Api.Tests",
                ["Jwt:Audience"] = "ProjectManager.Client.Tests",
                ["Jwt:AccessTokenMinutes"] = "30"
            })
            .Build();
    }

    internal static Mock<UserManager<ApplicationUser>> CreateUserManager()
    {
        return new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
    }

    internal static Mock<RoleManager<IdentityRole>> CreateRoleManager()
    {
        return new Mock<RoleManager<IdentityRole>>(
            Mock.Of<IRoleStore<IdentityRole>>(),
            null!,
            null!,
            null!,
            null!);
    }
}


