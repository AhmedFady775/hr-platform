using System.Security.Claims;
using HrPlatform.Contracts.Dtos.Auth;
using HrPlatform.Web.Components;
using HrPlatform.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// the Data Protection key ring defaults to living inside the container's own filesystem, which
// docker recreates from scratch on every restart/rebuild -- every sign-in cookie issued before
// that point becomes undecryptable and everyone gets silently bounced back to /login. Persisting
// keys to a mounted volume (docker-compose.yml, DataProtection:KeysPath) survives restarts;
// pinning the application name keeps the key ring's discriminator stable across image rebuilds
// too. Left unset (e.g. in tests, or `dotnet run` outside Docker), Data Protection falls back to
// its normal ephemeral default -- there's no volume to point it at in those environments anyway.
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
        .SetApplicationName("HrPlatform.Web");
}

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "LeaveRequestsAuth";
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddHttpClient<LeaveRequestsApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["LeaveRequestsApi:BaseUrl"] ?? "https://localhost:7212/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/login-submit", async (HttpContext http, LeaveRequestsApiClient api) =>
{
    var form = await http.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();

    // required attributes are client-side only, so guard against an empty direct POST too
    var usernameEmpty = string.IsNullOrWhiteSpace(username);
    var passwordEmpty = string.IsNullOrWhiteSpace(password);
    if (usernameEmpty || passwordEmpty)
    {
        var field = usernameEmpty && passwordEmpty ? "both" : usernameEmpty ? "username" : "password";
        var msg = usernameEmpty && passwordEmpty
            ? "Username and password are required."
            : usernameEmpty ? "Username is required." : "Password is required.";
        return Results.Redirect($"/login?error={Uri.EscapeDataString(msg)}&field={field}");
    }

    try
    {
        var result = await api.LoginAsync(new LoginRequestDto { Username = username, Password = password });

        if (result.Role != "HR")
        {
            var msg = $"This system is for HR staff only. \"{result.Username}\" is a {result.Role} account and can't access it.";
            return Results.Redirect($"/login?error={Uri.EscapeDataString(msg)}");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, result.Username),
            new(ClaimTypes.Role, result.Role),
            new("api_jwt", result.Token)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return Results.Redirect("/");
    }
    catch (ApiException ex) when (ex.StatusCode == 401)
    {
        // API doesn't say which field was wrong (avoids leaking valid usernames), so flag both
        return Results.Redirect($"/login?error={Uri.EscapeDataString(ex.Message)}&field=both");
    }
    catch (ApiException ex)
    {
        return Results.Redirect($"/login?error={Uri.EscapeDataString(ex.Message)}");
    }
});

var logout = async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
};
app.MapPost("/logout", logout);
app.MapGet("/logout", logout);

app.Run();

public partial class Program
{
}
