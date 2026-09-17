using HrPlatform.Contracts.Enums.LeaveRequests;

namespace HrPlatform.Api.Models.LeaveRequests;

// EmployeeId points at a user id in the third-party HR system -- no local FK on purpose, see README
public class LeaveRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public LeaveType Type { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ReviewerNote { get; set; }
}
