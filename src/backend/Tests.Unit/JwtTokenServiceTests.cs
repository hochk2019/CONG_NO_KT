using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CongNoGolden.Api.Services;
using CongNoGolden.Application.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace Tests.Unit;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void CreateToken_IncludesPermissionClaims()
    {
        var service = new JwtTokenService(Options.Create(new JwtOptions
        {
            Secret = "test-secret-with-at-least-32-characters",
            Issuer = "test",
            Audience = "test",
            ExpiryMinutes = 60
        }));

        var result = service.CreateToken(
            Guid.NewGuid(),
            "tester",
            ["Accountant"],
            ["customer.edit.owned", "import.commit.invoice"]);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        var permissions = token.Claims
            .Where(claim => claim.Type == "permission")
            .Select(claim => claim.Value)
            .ToArray();

        Assert.Collection(
            permissions,
            permission => Assert.Equal("customer.edit.owned", permission),
            permission => Assert.Equal("import.commit.invoice", permission));
    }
}
