using HrPlatform.Api.Models.LeaveRequests;
using Microsoft.EntityFrameworkCore;

namespace HrPlatform.Api.Data.LeaveRequests;

public class LeaveRequestsDbContext : DbContext
{
    public LeaveRequestsDbContext(DbContextOptions<LeaveRequestsDbContext> options) : base(options)
    {
    }

    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.ToTable("LeaveRequests", t =>
            {
                t.HasCheckConstraint("CK_LeaveRequests_DateRange", "[EndDate] >= [StartDate]");
                t.HasCheckConstraint("CK_LeaveRequests_Status", "[Status] IN (0, 1, 2)");
                t.HasCheckConstraint("CK_LeaveRequests_Type", "[Type] IN (0, 1, 2)");
            });

            entity.HasKey(e => e.Id);
            entity.Property(e => e.EmployeeId).IsRequired();
            entity.Property(e => e.StartDate).IsRequired();
            entity.Property(e => e.EndDate).IsRequired();
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ReviewerNote).HasMaxLength(500);

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.EmployeeId);
        });
    }
}
