using HrPlatform.Api.Services.Auth;
using HrPlatform.Contracts.Dtos.Auth;
using HrPlatform.Contracts.Dtos.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrPlatform.Api.Controllers.Auth;

// issues JWTs for the demo accounts in DemoUsers -- stands in for a real identity provider
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;

    public AuthController(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    public ActionResult<LoginResponseDto> Login([FromBody] LoginRequestDto dto)
    {
        var user = DemoUsers.Find(dto.Username, dto.Password);
        if (user is null)
        {
            return Unauthorized(new ErrorResponseDto { Error = "Invalid username or password." });
        }

        var (token, expiresAtUtc) = _tokenService.GenerateToken(user.Username, user.Role);

        return Ok(new LoginResponseDto
        {
            Token = token,
            Username = user.Username,
            Role = user.Role,
            ExpiresAtUtc = expiresAtUtc
        });
    }
}
