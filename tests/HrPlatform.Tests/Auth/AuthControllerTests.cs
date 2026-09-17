using HrPlatform.Api.Controllers.Auth;
using HrPlatform.Api.Models.Auth;
using HrPlatform.Api.Services.Auth;
using HrPlatform.Contracts.Dtos.Auth;
using HrPlatform.Contracts.Dtos.Common;
using Microsoft.AspNetCore.Mvc;

namespace HrPlatform.Tests.Auth;

public class AuthControllerTests
{
    private class FakeTokenService : ITokenService
    {
        public (string Token, DateTime ExpiresAtUtc) GenerateToken(string username, string role) =>
            ($"fake-token-for-{username}", DateTime.UtcNow.AddHours(1));
    }

    private class FakeUserStore : IUserStore
    {
        public Task<User?> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default)
        {
            if (string.Equals(username, "hr@company.com", StringComparison.OrdinalIgnoreCase) && password == "Password123!")
            {
                return Task.FromResult<User?>(new User { Username = "hr@company.com", Role = "HR" });
            }

            return Task.FromResult<User?>(null);
        }
    }

    [Fact]
    public async Task Login_ReturnsUnauthorizedForBadCredentials()
    {
        var controller = new AuthController(new FakeUserStore(), new FakeTokenService());

        var result = await controller.Login(new LoginRequestDto { Username = "nobody@company.com", Password = "wrong" });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var error = Assert.IsType<ErrorResponseDto>(unauthorized.Value);
        Assert.Equal("Invalid username or password.", error.Error);
    }

    [Fact]
    public async Task Login_ReturnsTokenAndRoleForValidHrCredentials()
    {
        var controller = new AuthController(new FakeUserStore(), new FakeTokenService());

        var result = await controller.Login(new LoginRequestDto { Username = "hr@company.com", Password = "Password123!" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<LoginResponseDto>(ok.Value);
        Assert.Equal("hr@company.com", response.Username);
        Assert.Equal("HR", response.Role);
        Assert.Equal("fake-token-for-hr@company.com", response.Token);
    }

    [Fact]
    public async Task Login_UsernameIsCaseInsensitive()
    {
        var controller = new AuthController(new FakeUserStore(), new FakeTokenService());

        var result = await controller.Login(new LoginRequestDto { Username = "HR@COMPANY.COM", Password = "Password123!" });

        Assert.IsType<OkObjectResult>(result.Result);
    }
}
