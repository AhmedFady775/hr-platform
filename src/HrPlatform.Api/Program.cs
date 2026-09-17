using System.Text;
using HrPlatform.Api.Data.LeaveRequests;
using HrPlatform.Api.Middleware;
using HrPlatform.Api.Services.Auth;
using HrPlatform.Contracts.Dtos.Common;
using HrPlatform.Api.Services.Employees;
using HrPlatform.Api.Services.LeaveRequests;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

builder.Services.AddDbContext<LeaveRequestsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("HrPlatformDb")));

builder.Services.AddHttpClient<IEmployeeClient, EmployeeClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["EmployeeApi:BaseUrl"] ?? "https://dummyjson.com/");
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddScoped<ILeaveRequestService, LeaveRequestService>();
builder.Services.AddSingleton<ITokenService, TokenService>();

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSection["Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ErrorResponseDto
            {
                Error = "Authentication required. Sign in with an HR account."
            });
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole("HR")
        .Build();
});
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationMiddlewareResultHandler>();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LeaveRequestsDbContext>();
    await DbInitializer.MigrateAndSeedAsync(db);
}

app.Run();
