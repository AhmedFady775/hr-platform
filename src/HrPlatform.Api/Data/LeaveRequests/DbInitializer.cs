using HrPlatform.Api.Models.LeaveRequests;
using HrPlatform.Contracts.Enums.LeaveRequests;
using Microsoft.EntityFrameworkCore;

namespace HrPlatform.Api.Data.LeaveRequests;

public static class DbInitializer
{
    public static async Task MigrateAndSeedAsync(LeaveRequestsDbContext db)
    {
        // migrations are authored against SQL Server; any other provider (e.g. Sqlite in tests)
        // builds its schema straight from the current model instead of replaying SQL Server-specific SQL
        if (db.Database.IsSqlServer())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }

        if (await db.LeaveRequests.AnyAsync())
        {
            return;
        }

        db.LeaveRequests.AddRange(
            new LeaveRequest { EmployeeId = 1, StartDate = new DateOnly(2026, 1, 5), EndDate = new DateOnly(2026, 1, 9), Type = LeaveType.Vacation, Status = LeaveStatus.Approved, CreatedAt = new DateTime(2025, 12, 20, 9, 0, 0, DateTimeKind.Utc), ReviewerNote = "Approved - holiday coverage confirmed" },
            new LeaveRequest { EmployeeId = 2, StartDate = new DateOnly(2026, 1, 12), EndDate = new DateOnly(2026, 1, 12), Type = LeaveType.Sick, Status = LeaveStatus.Approved, CreatedAt = new DateTime(2026, 1, 11, 8, 15, 0, DateTimeKind.Utc), ReviewerNote = "Approved - single sick day" },
            new LeaveRequest { EmployeeId = 3, StartDate = new DateOnly(2026, 2, 1), EndDate = new DateOnly(2026, 2, 5), Type = LeaveType.Vacation, Status = LeaveStatus.Pending, CreatedAt = new DateTime(2026, 1, 15, 14, 30, 0, DateTimeKind.Utc) },
            new LeaveRequest { EmployeeId = 4, StartDate = new DateOnly(2026, 2, 10), EndDate = new DateOnly(2026, 2, 11), Type = LeaveType.Unpaid, Status = LeaveStatus.Rejected, CreatedAt = new DateTime(2026, 1, 20, 10, 0, 0, DateTimeKind.Utc), ReviewerNote = "Rejected - insufficient notice for unpaid leave" },
            new LeaveRequest { EmployeeId = 5, StartDate = new DateOnly(2026, 2, 20), EndDate = new DateOnly(2026, 2, 27), Type = LeaveType.Vacation, Status = LeaveStatus.Pending, CreatedAt = new DateTime(2026, 2, 1, 11, 45, 0, DateTimeKind.Utc) },
            new LeaveRequest { EmployeeId = 6, StartDate = new DateOnly(2026, 3, 3), EndDate = new DateOnly(2026, 3, 4), Type = LeaveType.Sick, Status = LeaveStatus.Pending, CreatedAt = new DateTime(2026, 2, 25, 16, 20, 0, DateTimeKind.Utc) },
            new LeaveRequest { EmployeeId = 7, StartDate = new DateOnly(2026, 3, 15), EndDate = new DateOnly(2026, 3, 19), Type = LeaveType.Vacation, Status = LeaveStatus.Approved, CreatedAt = new DateTime(2026, 2, 28, 9, 30, 0, DateTimeKind.Utc), ReviewerNote = "Approved" },
            new LeaveRequest { EmployeeId = 8, StartDate = new DateOnly(2026, 3, 22), EndDate = new DateOnly(2026, 3, 22), Type = LeaveType.Sick, Status = LeaveStatus.Approved, CreatedAt = new DateTime(2026, 3, 21, 7, 50, 0, DateTimeKind.Utc), ReviewerNote = "Approved - doctor note on file" },
            new LeaveRequest { EmployeeId = 9, StartDate = new DateOnly(2026, 4, 1), EndDate = new DateOnly(2026, 4, 10), Type = LeaveType.Unpaid, Status = LeaveStatus.Pending, CreatedAt = new DateTime(2026, 3, 10, 13, 0, 0, DateTimeKind.Utc) },
            new LeaveRequest { EmployeeId = 10, StartDate = new DateOnly(2026, 4, 15), EndDate = new DateOnly(2026, 4, 16), Type = LeaveType.Vacation, Status = LeaveStatus.Rejected, CreatedAt = new DateTime(2026, 3, 25, 15, 10, 0, DateTimeKind.Utc), ReviewerNote = "Rejected - conflicts with quarter close" }
        );

        await db.SaveChangesAsync();
    }
}
