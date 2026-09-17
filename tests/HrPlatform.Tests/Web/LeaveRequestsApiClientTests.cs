extern alias WebAssembly;

using System.Net;
using HrPlatform.Contracts.Dtos.Auth;
using HrPlatform.Contracts.Enums.LeaveRequests;
using HrPlatform.Contracts.Dtos.LeaveRequests;
using WebAssembly::HrPlatform.Web.Services;

namespace HrPlatform.Tests.Web;

public class LeaveRequestsApiClientTests
{
    private class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public HttpRequestMessage? LastRequest { get; private set; }

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_respond(request));
        }
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private static (LeaveRequestsApiClient Client, FakeHandler Handler) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond, string? jwt = "test-jwt")
    {
        var handler = new FakeHandler(respond);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://fake-api/") };
        var client = new LeaveRequestsApiClient(httpClient, new FakeAuthenticationStateProvider(jwt));
        return (client, handler);
    }

    [Fact]
    public async Task GetLeaveRequestsAsync_AttachesBearerTokenFromAuthState()
    {
        var (client, handler) = CreateClient(_ => JsonResponse(HttpStatusCode.OK, """{ "items": [], "page": 1, "pageSize": 20, "totalCount": 0 }"""));

        await client.GetLeaveRequestsAsync(null, null, 1, 20);

        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("test-jwt", handler.LastRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task GetLeaveRequestsAsync_OmitsAuthorizationHeaderWhenNoTokenClaim()
    {
        var (client, handler) = CreateClient(_ => JsonResponse(HttpStatusCode.OK, """{ "items": [], "page": 1, "pageSize": 20, "totalCount": 0 }"""), jwt: null);

        await client.GetLeaveRequestsAsync(null, null, 1, 20);

        Assert.Null(handler.LastRequest!.Headers.Authorization);
    }

    [Fact]
    public async Task GetLeaveRequestsAsync_IncludesStatusAndEmployeeIdInQueryString()
    {
        var (client, handler) = CreateClient(_ => JsonResponse(HttpStatusCode.OK, """{ "items": [], "page": 1, "pageSize": 20, "totalCount": 0 }"""));

        await client.GetLeaveRequestsAsync(LeaveStatus.Approved, 7, 2, 10);

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("status=Approved", query);
        Assert.Contains("employeeId=7", query);
        Assert.Contains("page=2", query);
        Assert.Contains("pageSize=10", query);
    }

    [Fact]
    public async Task LoginAsync_ReturnsParsedResponseOnSuccess()
    {
        var (client, _) = CreateClient(_ => JsonResponse(HttpStatusCode.OK, """
            { "token": "abc", "username": "hr@company.com", "role": "HR", "expiresAtUtc": "2026-01-01T00:00:00Z" }
            """), jwt: null);

        var result = await client.LoginAsync(new LoginRequestDto { Username = "hr@company.com", Password = "Password123!" });

        Assert.Equal("abc", result.Token);
        Assert.Equal("HR", result.Role);
    }

    [Fact]
    public async Task LoginAsync_ThrowsApiExceptionWithStatusCodeAndServerMessageOnFailure()
    {
        var (client, _) = CreateClient(_ => JsonResponse(HttpStatusCode.Unauthorized, """{ "error": "Invalid username or password." }"""), jwt: null);

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.LoginAsync(new LoginRequestDto { Username = "hr@company.com", Password = "wrong" }));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("Invalid username or password.", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_FallsBackToGenericMessageWhenErrorBodyIsNotTheExpectedShape()
    {
        var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("not json", System.Text.Encoding.UTF8, "text/plain")
        }, jwt: null);

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.LoginAsync(new LoginRequestDto { Username = "hr@company.com", Password = "wrong" }));

        Assert.Equal(500, ex.StatusCode);
        Assert.Equal("Request failed with status 500.", ex.Message);
    }

    [Fact]
    public async Task SendAsync_WrapsTimeoutAsApiException()
    {
        var (client, _) = CreateClient(_ => throw new TaskCanceledException());

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetLeaveRequestsAsync(null, null, 1, 20));

        Assert.Equal("The request to the server timed out.", ex.Message);
        Assert.Null(ex.StatusCode);
    }

    [Fact]
    public async Task SendAsync_WrapsHttpRequestExceptionAsApiException()
    {
        var (client, _) = CreateClient(_ => throw new HttpRequestException("connection refused"));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetLeaveRequestsAsync(null, null, 1, 20));

        Assert.Contains("Could not reach the server", ex.Message);
    }

    [Fact]
    public async Task DeleteLeaveRequestAsync_SendsDeleteAndDoesNotThrowOnNoContent()
    {
        var (client, handler) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        await client.DeleteLeaveRequestAsync(5);

        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
        Assert.Contains("api/leave-requests/5", handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateLeaveRequestAsync_SerializesEnumAsStringAndReturnsCreatedDto()
    {
        var (client, handler) = CreateClient(_ => JsonResponse(HttpStatusCode.Created, """
            { "id": 1, "employeeId": 1, "startDate": "2026-01-01", "endDate": "2026-01-02", "type": "Sick", "status": "Pending", "createdAt": "2026-01-01T00:00:00Z" }
            """));

        var result = await client.CreateLeaveRequestAsync(new CreateLeaveRequestDto
        {
            EmployeeId = 1,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 1, 2),
            Type = LeaveType.Sick
        });

        var sentBody = await handler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("\"type\":\"Sick\"", sentBody);
        Assert.Equal(LeaveType.Sick, result.Type);
        Assert.Equal(LeaveStatus.Pending, result.Status);
    }
}
