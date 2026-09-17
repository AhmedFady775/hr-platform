using System.Text.Json;
using System.Text.Json.Serialization;
using HrPlatform.Api.Middleware;
using HrPlatform.Contracts.Dtos.Employees;
using Microsoft.Extensions.Caching.Distributed;

namespace HrPlatform.Api.Services.Employees;

public interface IEmployeeClient
{
    Task<EmployeeDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<EmployeeListResultDto> ListAsync(int limit, int skip, string? search, CancellationToken ct = default);

    Task<Dictionary<int, EmployeeDto>> GetAllAsLookupAsync(CancellationToken ct = default);
}

// talks to dummyjson.com/users, with a short distributed cache (shared across API instances)
// so a page of leave requests doesn't re-fetch employee data per row
public class EmployeeClient : IEmployeeClient
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly ILogger<EmployeeClient> _logger;

    private const string AllEmployeesCacheKey = "employees:all";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    public EmployeeClient(HttpClient httpClient, IDistributedCache cache, ILogger<EmployeeClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<EmployeeDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        // if we already have the full list cached, use it instead of a round trip
        var cached = await GetCachedLookupAsync(ct);
        if (cached is not null && cached.TryGetValue(id, out var fromCache))
        {
            return fromCache;
        }

        try
        {
            var response = await _httpClient.GetAsync($"users/{id}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var raw = await response.Content.ReadFromJsonAsync<RemoteUser>(cancellationToken: ct);
            return raw is null ? null : Map(raw);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new EmployeeServiceUnavailableException("The employee system timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new EmployeeServiceUnavailableException("The employee system is unavailable.", ex);
        }
    }

    public async Task<EmployeeListResultDto> ListAsync(int limit, int skip, string? search, CancellationToken ct = default)
    {
        try
        {
            var path = string.IsNullOrWhiteSpace(search)
                ? $"users?limit={limit}&skip={skip}"
                : $"users/search?q={Uri.EscapeDataString(search)}&limit={limit}&skip={skip}";

            var response = await _httpClient.GetAsync(path, ct);
            response.EnsureSuccessStatusCode();
            var raw = await response.Content.ReadFromJsonAsync<RemoteUserListResponse>(cancellationToken: ct);

            return new EmployeeListResultDto
            {
                Employees = raw?.Users.Select(Map).ToList() ?? new List<EmployeeDto>(),
                Total = raw?.Total ?? 0
            };
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new EmployeeServiceUnavailableException("The employee system timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new EmployeeServiceUnavailableException("The employee system is unavailable.", ex);
        }
    }

    public async Task<Dictionary<int, EmployeeDto>> GetAllAsLookupAsync(CancellationToken ct = default)
    {
        var cached = await GetCachedLookupAsync(ct);
        if (cached is not null)
        {
            return cached;
        }

        try
        {
            // dummyjson caps a page at 100, and the mock dataset is exactly 100 users, so one call covers it
            var response = await _httpClient.GetAsync("users?limit=100&skip=0", ct);
            response.EnsureSuccessStatusCode();
            var raw = await response.Content.ReadFromJsonAsync<RemoteUserListResponse>(cancellationToken: ct);

            var lookup = (raw?.Users ?? new List<RemoteUser>())
                .Select(Map)
                .ToDictionary(e => e.Id, e => e);

            await _cache.SetAsync(
                AllEmployeesCacheKey,
                JsonSerializer.SerializeToUtf8Bytes(lookup),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheDuration },
                ct);
            return lookup;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Employee system timed out while fetching the full employee list.");
            throw new EmployeeServiceUnavailableException("The employee system timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Employee system is unavailable while fetching the full employee list.");
            throw new EmployeeServiceUnavailableException("The employee system is unavailable.", ex);
        }
    }

    private async Task<Dictionary<int, EmployeeDto>?> GetCachedLookupAsync(CancellationToken ct)
    {
        var bytes = await _cache.GetAsync(AllEmployeesCacheKey, ct);
        return bytes is null ? null : JsonSerializer.Deserialize<Dictionary<int, EmployeeDto>>(bytes);
    }

    private static EmployeeDto Map(RemoteUser u) => new()
    {
        Id = u.Id,
        FirstName = u.FirstName,
        LastName = u.LastName,
        Email = u.Email,
        Department = u.Company?.Department ?? string.Empty,
        Title = u.Company?.Title ?? string.Empty
    };

    private class RemoteUserListResponse
    {
        [JsonPropertyName("users")]
        public List<RemoteUser> Users { get; set; } = new();

        [JsonPropertyName("total")]
        public int Total { get; set; }
    }

    private class RemoteUser
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("firstName")]
        public string FirstName { get; set; } = string.Empty;

        [JsonPropertyName("lastName")]
        public string LastName { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("company")]
        public RemoteCompany? Company { get; set; }
    }

    private class RemoteCompany
    {
        [JsonPropertyName("department")]
        public string Department { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
    }
}

public class EmployeeServiceUnavailableException : Exception, IApiException
{
    public EmployeeServiceUnavailableException(string message, Exception inner) : base(message, inner)
    {
    }

    public int StatusCode => StatusCodes.Status502BadGateway;
    public string UserMessage => Message;
}
