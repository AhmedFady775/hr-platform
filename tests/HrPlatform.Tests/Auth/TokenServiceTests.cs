using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HrPlatform.Api.Services.Auth;
using Microsoft.Extensions.Configuration;

namespace HrPlatform.Tests.Auth;

public class TokenServiceTests
{
    private static TokenService CreateService(int? expiryMinutes = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "super-secret-test-signing-key-that-is-long-enough",
            ["Jwt:Issuer"] = "HrPlatform.Tests",
            ["Jwt:Audience"] = "HrPlatform.Tests.Client"
        };
        if (expiryMinutes is not null)
        {
            settings["Jwt:ExpiryMinutes"] = expiryMinutes.Value.ToString();
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new TokenService(configuration);
    }

    [Fact]
    public void GenerateToken_ThrowsWhenKeyIsMissing()
    {
        var configuration = new ConfigurationBuilder().Build();
        var service = new TokenService(configuration);

        Assert.Throws<InvalidOperationException>(() => service.GenerateToken("hr@company.com", "HR"));
    }

    [Fact]
    public void GenerateToken_IncludesUsernameAndRoleClaims()
    {
        var service = CreateService();

        var (token, _) = service.GenerateToken("hr@company.com", "HR");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("hr@company.com", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("HR", jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal("HrPlatform.Tests", jwt.Issuer);
        Assert.Equal("HrPlatform.Tests.Client", jwt.Audiences.Single());
    }

    [Fact]
    public void GenerateToken_DefaultsExpiryTo60MinutesWhenNotConfigured()
    {
        var service = CreateService();
        var before = DateTime.UtcNow;

        var (_, expiresAtUtc) = service.GenerateToken("hr@company.com", "HR");

        Assert.InRange(expiresAtUtc, before.AddMinutes(59), before.AddMinutes(61));
    }

    [Fact]
    public void GenerateToken_HonorsConfiguredExpiry()
    {
        var service = CreateService(expiryMinutes: 5);
        var before = DateTime.UtcNow;

        var (_, expiresAtUtc) = service.GenerateToken("hr@company.com", "HR");

        Assert.InRange(expiresAtUtc, before.AddMinutes(4), before.AddMinutes(6));
    }
}
