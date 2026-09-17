using HrPlatform.Contracts.Dtos.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HrPlatform.Api.Middleware;

// maps domain/service exceptions to the { "error": "..." } shape so controllers don't need try/catch everywhere
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (statusCode, message) = ex switch
            {
                IApiException apiException => (apiException.StatusCode, apiException.UserMessage),
                ArgumentException => (StatusCodes.Status400BadRequest, "The request was invalid."),
                KeyNotFoundException => (StatusCodes.Status404NotFound, "The requested resource was not found."),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "This record was changed by someone else. Please reload and try again."),
                DbUpdateException dbUpdateException when IsUniqueConstraintViolation(dbUpdateException)
                    => (StatusCodes.Status409Conflict, "A record with this information already exists."),
                DbUpdateException => (StatusCodes.Status400BadRequest, "The request could not be saved."),
                SqlException => (StatusCodes.Status503ServiceUnavailable, "The service is temporarily unavailable. Please try again shortly."),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
            }
            else
            {
                _logger.LogWarning(ex, "Handled exception ({StatusCode}) processing {Method} {Path}", statusCode, context.Request.Method, context.Request.Path);
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(new ErrorResponseDto { Error = message });
        }
    }

    // SQL Server error numbers for a unique index / unique constraint violation
    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException sqlException && sqlException.Number is 2601 or 2627;
}
