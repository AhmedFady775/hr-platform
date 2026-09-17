using HrPlatform.Contracts.Dtos.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace HrPlatform.Api.Middleware;

// gives a 403 the same { "error": "..." } shape instead of the framework's default empty body
// (401s are handled separately, via JwtBearerEvents.OnChallenge in Program.cs)
public class ApiAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ErrorResponseDto
            {
                Error = "You don't have permission to perform this action. HR role required."
            });
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
