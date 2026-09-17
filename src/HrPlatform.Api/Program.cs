using HrPlatform.Api.Data.LeaveRequests;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<LeaveRequestsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("HrPlatformDb")));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
