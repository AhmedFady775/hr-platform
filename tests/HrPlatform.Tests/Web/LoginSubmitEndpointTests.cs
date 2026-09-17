using System.Net;

namespace HrPlatform.Tests.Web;

public class LoginSubmitEndpointTests : IDisposable
{
    private readonly CustomWebFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient CreateClientNoRedirect() =>
        _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static FormUrlEncodedContent Form(string username, string password) => new(new Dictionary<string, string>
    {
        ["username"] = username,
        ["password"] = password
    });

    [Fact]
    public async Task LoginSubmit_WithEmptyUsernameAndPassword_RedirectsWithBothFieldError()
    {
        var client = CreateClientNoRedirect();

        var response = await client.PostAsync("/login-submit", Form("", ""));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("/login?", location);
        Assert.Contains("field=both", location);
    }

    [Fact]
    public async Task LoginSubmit_WithEmptyPasswordOnly_RedirectsWithPasswordFieldError()
    {
        var client = CreateClientNoRedirect();

        var response = await client.PostAsync("/login-submit", Form("hr@company.com", ""));

        var location = response.Headers.Location!.ToString();
        Assert.Contains("field=password", location);
    }

    [Fact]
    public async Task LoginSubmit_WithInvalidCredentials_RedirectsWithBothFieldError()
    {
        _factory.RespondToApi = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{ "error": "Invalid username or password." }""", System.Text.Encoding.UTF8, "application/json")
        };
        var client = CreateClientNoRedirect();

        var response = await client.PostAsync("/login-submit", Form("hr@company.com", "wrong"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("field=both", location);
        Assert.Contains("Invalid+username", location.Replace("%20", "+"));
    }

    [Fact]
    public async Task LoginSubmit_WithNonHrAccount_RedirectsWithRoleError()
    {
        _factory.RespondToApi = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "token": "tok", "username": "employee@company.com", "role": "Employee", "expiresAtUtc": "2026-01-01T00:00:00Z" }""", System.Text.Encoding.UTF8, "application/json")
        };
        var client = CreateClientNoRedirect();

        var response = await client.PostAsync("/login-submit", Form("employee@company.com", "Password123!"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("for HR staff only", Uri.UnescapeDataString(location));
        Assert.DoesNotContain("Set-Cookie", response.Headers.ToString());
    }

    [Fact]
    public async Task LoginSubmit_WithValidHrAccount_SignsInAndRedirectsHome()
    {
        _factory.RespondToApi = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "token": "tok", "username": "hr@company.com", "role": "HR", "expiresAtUtc": "2026-01-01T00:00:00Z" }""", System.Text.Encoding.UTF8, "application/json")
        };
        var client = CreateClientNoRedirect();

        var response = await client.PostAsync("/login-submit", Form("hr@company.com", "Password123!"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location!.ToString());
        Assert.True(response.Headers.Contains("Set-Cookie"));
        var cookie = response.Headers.GetValues("Set-Cookie").First();
        Assert.Contains("LeaveRequestsAuth", cookie);
    }

    [Fact]
    public async Task Logout_ClearsCookieAndRedirectsToLogin()
    {
        var client = CreateClientNoRedirect();

        var response = await client.PostAsync("/logout", new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login", response.Headers.Location!.ToString());
    }
}
