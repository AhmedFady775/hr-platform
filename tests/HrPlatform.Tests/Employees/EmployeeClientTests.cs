using System.Net;
using HrPlatform.Api.Services.Employees;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HrPlatform.Tests.Employees;

public class EmployeeClientTests
{
    private class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_respond(request));
    }

    private static EmployeeClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond, IDistributedCache? cache = null)
    {
        var httpClient = new HttpClient(new FakeHandler(respond)) { BaseAddress = new Uri("https://dummyjson.com/") };
        return new EmployeeClient(httpClient, cache ?? new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), NullLogger<EmployeeClient>.Instance);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenRemoteRespondsNotFound()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await client.GetByIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_MapsRemoteUserToEmployeeDto()
    {
        var client = CreateClient(_ => JsonResponse(HttpStatusCode.OK, """
            { "id": 1, "firstName": "Ada", "lastName": "Lovelace", "email": "ada@company.com", "company": { "department": "Engineering", "title": "Engineer" } }
            """));

        var result = await client.GetByIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal("Ada Lovelace", result!.FullName);
        Assert.Equal("Engineering", result.Department);
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsEmployeeServiceUnavailableOnHttpFailure()
    {
        var client = CreateClient(_ => throw new HttpRequestException("connection refused"));

        await Assert.ThrowsAsync<EmployeeServiceUnavailableException>(() => client.GetByIdAsync(1));
    }

    [Fact]
    public async Task GetByIdAsync_UsesCachedLookupInsteadOfCallingRemote()
    {
        var callCount = 0;
        var client = CreateClient(_ =>
        {
            callCount++;
            return JsonResponse(HttpStatusCode.OK, """{ "users": [ { "id": 1, "firstName": "Ada", "lastName": "Lovelace", "email": "ada@company.com" } ], "total": 1 }""");
        });

        await client.GetAllAsLookupAsync();
        var result = await client.GetByIdAsync(1);

        Assert.Equal(1, callCount);
        Assert.NotNull(result);
        Assert.Equal("Ada", result!.FirstName);
    }

    [Fact]
    public async Task GetAllAsLookupAsync_CachesResultAcrossCalls()
    {
        var callCount = 0;
        var client = CreateClient(_ =>
        {
            callCount++;
            return JsonResponse(HttpStatusCode.OK, """{ "users": [ { "id": 1, "firstName": "Ada", "lastName": "Lovelace", "email": "ada@company.com" } ], "total": 1 }""");
        });

        var first = await client.GetAllAsLookupAsync();
        var second = await client.GetAllAsLookupAsync();

        // a distributed cache round-trips through serialization, so the second call never
        // returns the same instance -- assert on content instead of reference identity
        Assert.Equal(1, callCount);
        Assert.Equal(first.Keys, second.Keys);
        Assert.Equal(first[1].FullName, second[1].FullName);
    }

    [Fact]
    public async Task GetAllAsLookupAsync_ThrowsEmployeeServiceUnavailableOnHttpFailure()
    {
        var client = CreateClient(_ => throw new HttpRequestException("connection refused"));

        await Assert.ThrowsAsync<EmployeeServiceUnavailableException>(() => client.GetAllAsLookupAsync());
    }

    [Fact]
    public async Task ListAsync_MapsUsersAndTotal()
    {
        var client = CreateClient(_ => JsonResponse(HttpStatusCode.OK, """
            { "users": [ { "id": 1, "firstName": "Ada", "lastName": "Lovelace", "email": "ada@company.com" } ], "total": 42 }
            """));

        var result = await client.ListAsync(10, 0, null);

        Assert.Equal(42, result.Total);
        Assert.Single(result.Employees);
    }
}
