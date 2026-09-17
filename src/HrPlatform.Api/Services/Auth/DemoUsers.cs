namespace HrPlatform.Api.Services.Auth;

public record DemoUser(string Username, string Password, string Role);

// hardcoded creds standing in for a real identity provider -- just enough to demo role-gated actions
public static class DemoUsers
{
    private static readonly DemoUser[] Users =
    {
        new("hr@company.com", "Password123!", "HR"),
        new("employee@company.com", "Password123!", "Employee")
    };

    public static DemoUser? Find(string username, string password) =>
        Users.FirstOrDefault(u =>
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase) &&
            u.Password == password);
}
