using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HrPlatform.Contracts.Dtos.Auth;
using HrPlatform.Contracts.Dtos.Common;
using HrPlatform.Contracts.Dtos.Employees;
using HrPlatform.Contracts.Enums.LeaveRequests;
using HrPlatform.Contracts.Dtos.LeaveRequests;
using Microsoft.AspNetCore.Components.Authorization;

namespace HrPlatform.Web.Services;

public class ApiException : Exception
{
    public int? StatusCode { get; }

    public ApiException(string message, int? statusCode = null) : base(message)
    {
        StatusCode = statusCode;
    }
}

// every endpoint but login needs a bearer token, so every request here attaches one pulled
// from the signed-in user's auth cookie (no-op if there isn't one -- the API just 401s)
public class LeaveRequestsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AuthenticationStateProvider _authStateProvider;

    // matches the API's JsonStringEnumConverter so enums round-trip as strings
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public LeaveRequestsApiClient(HttpClient httpClient, AuthenticationStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _authStateProvider = authStateProvider;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken ct = default)
    {
        var response = await SendAsync(() => _httpClient.PostAsJsonAsync("api/auth/login", dto, JsonOptions, ct));
        return await ReadOrThrowAsync<LoginResponseDto>(response, ct)
               ?? throw new ApiException("Empty response from server.");
    }

    public async Task<PagedResultDto<LeaveRequestDto>> GetLeaveRequestsAsync(
        LeaveStatus? status, int? employeeId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (status.HasValue) query.Add($"status={status.Value}");
        if (employeeId.HasValue) query.Add($"employeeId={employeeId.Value}");

        var response = await SendAsync(() => Get($"api/leave-requests?{string.Join("&", query)}", ct));
        return await ReadOrThrowAsync<PagedResultDto<LeaveRequestDto>>(response, ct)
               ?? new PagedResultDto<LeaveRequestDto>();
    }

    public async Task<LeaveRequestDto> CreateLeaveRequestAsync(CreateLeaveRequestDto dto, CancellationToken ct = default)
    {
        var response = await SendAsync(() => Send(HttpMethod.Post, "api/leave-requests", dto, ct));
        return await ReadOrThrowAsync<LeaveRequestDto>(response, ct)
               ?? throw new ApiException("Empty response from server.");
    }

    public async Task<LeaveRequestDto> UpdateStatusAsync(int id, UpdateLeaveStatusDto dto, CancellationToken ct = default)
    {
        var response = await SendAsync(() => Send(HttpMethod.Put, $"api/leave-requests/{id}/status", dto, ct));
        return await ReadOrThrowAsync<LeaveRequestDto>(response, ct)
               ?? throw new ApiException("Empty response from server.");
    }

    public async Task DeleteLeaveRequestAsync(int id, CancellationToken ct = default)
    {
        var response = await SendAsync(() => Send(HttpMethod.Delete, $"api/leave-requests/{id}", null, ct));
        await ReadOrThrowAsync<object>(response, ct);
    }

    public async Task<EmployeeListResultDto> GetEmployeesAsync(string? search = null, int limit = 20, CancellationToken ct = default)
    {
        var query = $"limit={limit}" + (string.IsNullOrWhiteSpace(search) ? "" : $"&search={Uri.EscapeDataString(search)}");
        var response = await SendAsync(() => Get($"api/employees?{query}", ct));
        return await ReadOrThrowAsync<EmployeeListResultDto>(response, ct)
               ?? new EmployeeListResultDto();
    }

    private async Task<HttpResponseMessage> Get(string url, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        await ApplyAuthAsync(request);
        return await _httpClient.SendAsync(request, ct);
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
        }
        await ApplyAuthAsync(request);
        return await _httpClient.SendAsync(request, ct);
    }

    private async Task ApplyAuthAsync(HttpRequestMessage request)
    {
        var state = await _authStateProvider.GetAuthenticationStateAsync();
        var token = state.User.FindFirst("api_jwt")?.Value;
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private static async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            return await send();
        }
        catch (TaskCanceledException)
        {
            throw new ApiException("The request to the server timed out.");
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException($"Could not reach the server: {ex.Message}");
        }
    }

    private static async Task<T?> ReadOrThrowAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }

        string message = $"Request failed with status {(int)response.StatusCode}.";
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>(JsonOptions, ct);
            if (error is not null && !string.IsNullOrWhiteSpace(error.Error))
            {
                message = error.Error;
            }
        }
        catch
        {
            // wasn't the expected error shape, just use the generic message
        }

        throw new ApiException(message, (int)response.StatusCode);
    }
}
