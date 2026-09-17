using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace HrPlatform.Tests.Web;

public class FakeAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ClaimsPrincipal _user;

    public FakeAuthenticationStateProvider(string? jwt = null)
    {
        var claims = jwt is null ? Array.Empty<Claim>() : new[] { new Claim("api_jwt", jwt) };
        _user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(_user));
}
