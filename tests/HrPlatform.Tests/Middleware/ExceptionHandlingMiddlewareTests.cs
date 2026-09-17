using System.Text.Json;
using HrPlatform.Api.Middleware;
using HrPlatform.Api.Services.Employees;
using HrPlatform.Api.Services.LeaveRequests;
using HrPlatform.Contracts.Dtos.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace HrPlatform.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int StatusCode, string Error)> InvokeAsync(Exception exception)
    {
        var middleware = new ExceptionHandlingMiddleware(_ => throw exception, NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var error = JsonSerializer.Deserialize<ErrorResponseDto>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return (context.Response.StatusCode, error!.Error);
    }

    [Fact]
    public async Task MapsLeaveRequestNotFoundException_To404()
    {
        var (statusCode, error) = await InvokeAsync(new LeaveRequestNotFoundException());

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
        Assert.Equal("Leave request not found.", error);
    }

    [Fact]
    public async Task MapsEmployeeNotFoundException_To404WithMessage()
    {
        var (statusCode, error) = await InvokeAsync(new EmployeeNotFoundException(42));

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
        Assert.Contains("42", error);
    }

    [Fact]
    public async Task MapsInvalidStatusTransitionException_To409()
    {
        var (statusCode, _) = await InvokeAsync(new InvalidStatusTransitionException("bad transition"));

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
    }

    [Fact]
    public async Task MapsLeaveRequestNotDeletableException_To400()
    {
        var (statusCode, _) = await InvokeAsync(new LeaveRequestNotDeletableException("cannot delete"));

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
    }

    [Fact]
    public async Task MapsArgumentException_To400()
    {
        var (statusCode, _) = await InvokeAsync(new ArgumentException("bad argument"));

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
    }

    [Fact]
    public async Task MapsEmployeeServiceUnavailableException_To502()
    {
        var (statusCode, _) = await InvokeAsync(new EmployeeServiceUnavailableException("down", new Exception()));

        Assert.Equal(StatusCodes.Status502BadGateway, statusCode);
    }

    [Fact]
    public async Task MapsUnexpectedException_To500WithGenericMessage()
    {
        var (statusCode, error) = await InvokeAsync(new InvalidOperationException("boom, leaks internal detail"));

        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.Equal("An unexpected error occurred.", error);
    }
}
