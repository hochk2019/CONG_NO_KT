using System.Security.Claims;
using CongNoGolden.Api.Services;
using CongNoGolden.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Tests.Unit;

public sealed class CurrentUserServiceTests
{
    [Fact]
    public void Permissions_ReturnPermissionClaims_FromHttpContextUser()
    {
        var userId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "tester"),
            new Claim("permission", "customer.edit.owned"),
            new Claim("permission", "import.commit.invoice")
        };
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        ICurrentUser currentUser = new CurrentUserService(accessor);

        Assert.Equal(
            ["customer.edit.owned", "import.commit.invoice"],
            currentUser.Permissions);
    }
}
