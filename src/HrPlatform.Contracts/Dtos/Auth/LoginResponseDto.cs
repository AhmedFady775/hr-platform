namespace HrPlatform.Contracts.Dtos.Auth;

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}
