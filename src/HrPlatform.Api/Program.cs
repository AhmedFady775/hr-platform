using HrPlatform.Api.Data.LeaveRequests;
using HrPlatform.Api.Services.Employees;
using HrPlatform.Api.Services.LeaveRequests;
using Microsoft.EntityFrameworkCore;

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

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LeaveRequestsDbContext>();
    await DbInitializer.MigrateAndSeedAsync(db);
}

app.Run();
