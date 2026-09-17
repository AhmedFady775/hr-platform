using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HrPlatform.Contracts.Dtos.Auth;
using HrPlatform.Contracts.Dtos.Common;
using HrPlatform.Contracts.Enums.LeaveRequests;
using HrPlatform.Contracts.Dtos.LeaveRequests;

namespace HrPlatform.Tests.Integration;

// each test gets its own factory (own in-memory Sqlite database, freshly migrated and seeded)
// so tests that mutate data can't interfere with each other regardless of xunit's run order
public class LeaveRequestsApiTests : IDisposable
{
    // the API serializes enums as strings, so responses need the matching converter to deserialize
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CustomWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string username, string password)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Username = username, Password = password });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponseDto>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Username = "hr@company.com", Password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetLeaveRequests_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/leave-requests");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetLeaveRequests_WithHrToken_ReturnsSeededData()
    {
        var client = await CreateAuthenticatedClientAsync("hr@company.com", "Password123!");

        var response = await client.GetAsync("/api/leave-requests");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResultDto<LeaveRequestDto>>(JsonOptions);
        Assert.Equal(10, body!.TotalCount);
    }

    [Fact]
    public async Task UpdateStatus_WithEmployeeRoleToken_Returns403()
    {
        var client = await CreateAuthenticatedClientAsync("employee@company.com", "Password123!");

        var response = await client.PutAsJsonAsync("/api/leave-requests/3/status", new UpdateLeaveStatusDto { Status = LeaveStatus.Approved });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_WithHrToken_ApprovesPendingRequest()
    {
        var client = await CreateAuthenticatedClientAsync("hr@company.com", "Password123!");

        var response = await client.PutAsJsonAsync("/api/leave-requests/3/status", new UpdateLeaveStatusDto { Status = LeaveStatus.Approved, ReviewerNote = "Approved via test" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LeaveRequestDto>(JsonOptions);
        Assert.Equal(LeaveStatus.Approved, body!.Status);
    }

    [Fact]
    public async Task UpdateStatus_OnAlreadyApprovedRequest_Returns409()
    {
        var client = await CreateAuthenticatedClientAsync("hr@company.com", "Password123!");

        var response = await client.PutAsJsonAsync("/api/leave-requests/1/status", new UpdateLeaveStatusDto { Status = LeaveStatus.Rejected });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithEmployeeRoleToken_Returns403()
    {
        var client = await CreateAuthenticatedClientAsync("employee@company.com", "Password123!");

        var response = await client.DeleteAsync("/api/leave-requests/6");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithHrToken_OnPendingRequest_Returns204()
    {
        var client = await CreateAuthenticatedClientAsync("hr@company.com", "Password123!");

        var response = await client.DeleteAsync("/api/leave-requests/6");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_OnApprovedRequest_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync("hr@company.com", "Password123!");

        var response = await client.DeleteAsync("/api/leave-requests/2");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ForMissingRequest_Returns404()
    {
        var client = await CreateAuthenticatedClientAsync("hr@company.com", "Password123!");

        var response = await client.GetAsync("/api/leave-requests/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithInvalidBody_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync("hr@company.com", "Password123!");

        var response = await client.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto
        {
            EmployeeId = 1,
            StartDate = new DateOnly(2026, 5, 10),
            EndDate = new DateOnly(2026, 5, 1),
            Type = LeaveType.Vacation
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithUnknownEmployeeId_Returns404()
    {
        var client = await CreateAuthenticatedClientAsync("hr@company.com", "Password123!");

        var response = await client.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto
        {
            EmployeeId = 999,
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 5, 2),
            Type = LeaveType.Vacation
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithValidRequest_Returns201AndPendingStatus()
    {
        var client = await CreateAuthenticatedClientAsync("hr@company.com", "Password123!");

        var response = await client.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto
        {
            EmployeeId = 1,
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 5, 2),
            Type = LeaveType.Vacation
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LeaveRequestDto>(JsonOptions);
        Assert.Equal(LeaveStatus.Pending, body!.Status);
    }
}
