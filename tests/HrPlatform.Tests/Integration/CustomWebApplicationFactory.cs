using HrPlatform.Api.Data.LeaveRequests;
using HrPlatform.Api.Services.Employees;
using HrPlatform.Contracts.Dtos.Employees;
using HrPlatform.Tests.Employees;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace HrPlatform.Tests.Integration;

// swaps the real SQL Server DbContext for an in-memory Sqlite connection (so migrations still run,
// unlike the EF InMemory provider), the real Redis-backed IDistributedCache for an in-process one,
// and the real dummyjson-backed IEmployeeClient for a fake, so the test suite never touches a real
// database, Redis, or the network. Production migrates via a separate `--migrate-only` run (see
// Program.cs) rather than on API startup, so this factory runs migrate+seed itself instead of
// relying on that path.
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public CustomWebApplicationFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<LeaveRequestsDbContext>>();
            services.AddDbContext<LeaveRequestsDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();

            services.RemoveAll<IEmployeeClient>();
            services.RemoveAll<IHttpMessageHandlerBuilderFilter>();
            services.AddSingleton<IEmployeeClient>(new FakeEmployeeClient(
                Enumerable.Range(1, 10).Select(id => new EmployeeDto
                {
                    Id = id,
                    FirstName = $"Employee{id}",
                    LastName = "Test",
                    Email = $"employee{id}@company.com",
                    Department = "Engineering",
                    Title = "Engineer"
                }).ToArray()));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LeaveRequestsDbContext>();
        DbInitializer.MigrateAndSeedAsync(db).GetAwaiter().GetResult();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
