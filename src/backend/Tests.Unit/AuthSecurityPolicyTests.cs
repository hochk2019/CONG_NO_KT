using CongNoGolden.Api.Security;
using CongNoGolden.Application.Auth;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Tests.Unit;

public sealed class AuthSecurityPolicyTests
{
    [Fact]
    public void ValidateJwtOptions_Throws_WhenSecretTooShort()
    {
        var options = new JwtOptions { Secret = "short" };

        var ex = Assert.Throws<InvalidOperationException>(
            () => AuthSecurityPolicy.ValidateJwtOptions(options, isDevelopment: true));

        Assert.Contains("at least", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateJwtOptions_Throws_WhenPlaceholderUsedOutsideDevelopment()
    {
        var options = new JwtOptions { Secret = JwtOptions.SecretPlaceholder };

        var ex = Assert.Throws<InvalidOperationException>(
            () => AuthSecurityPolicy.ValidateJwtOptions(options, isDevelopment: false));

        Assert.Contains("placeholder", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateJwtOptions_Throws_WhenSecretMissingOutsideDevelopment()
    {
        var options = new JwtOptions { Secret = string.Empty };

        var ex = Assert.Throws<InvalidOperationException>(
            () => AuthSecurityPolicy.ValidateJwtOptions(options, isDevelopment: false));

        Assert.Contains(JwtOptions.SecretEnvironmentVariable, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateJwtOptions_AllowsStrongSecret()
    {
        var options = new JwtOptions { Secret = "this_is_a_valid_random_secret_with_32_chars+" };

        AuthSecurityPolicy.ValidateJwtOptions(options, isDevelopment: false);
    }

    [Fact]
    public void ValidatePasswordComplexity_Throws_WhenPasswordIsWeak()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => AuthSecurityPolicy.ValidatePasswordComplexity("weakpass"));

        Assert.Contains("uppercase", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePasswordComplexity_AllowsStrongPassword()
    {
        AuthSecurityPolicy.ValidatePasswordComplexity("StrongPass123");
    }

    [Theory]
    [InlineData(null, SameSiteMode.Strict)]
    [InlineData("", SameSiteMode.Strict)]
    [InlineData("Strict", SameSiteMode.Strict)]
    [InlineData("Lax", SameSiteMode.Lax)]
    [InlineData("None", SameSiteMode.None)]
    [InlineData("Unspecified", SameSiteMode.Unspecified)]
    [InlineData("unknown", SameSiteMode.Strict)]
    public void ResolveSameSiteMode_MapsExpectedValues(string? raw, SameSiteMode expected)
    {
        var actual = AuthSecurityPolicy.ResolveSameSiteMode(raw);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null, "/auth")]
    [InlineData("", "/auth")]
    [InlineData("/auth", "/auth")]
    [InlineData("auth", "/auth")]
    [InlineData("/api/auth", "/api/auth")]
    public void ResolveCookiePath_NormalizesValue(string? raw, string expected)
    {
        var actual = AuthSecurityPolicy.ResolveCookiePath(raw);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ResolveClientPartitionKey_ReturnsUnknown_WhenRemoteAddressMissing()
    {
        var context = new DefaultHttpContext();

        var key = AuthSecurityPolicy.ResolveClientPartitionKey(context);

        Assert.Equal("unknown", key);
    }
}
