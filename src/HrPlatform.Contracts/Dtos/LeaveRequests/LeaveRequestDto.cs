using HrPlatform.Contracts.Enums.LeaveRequests;

namespace HrPlatform.Contracts.Dtos.LeaveRequests;

public class LeaveRequestDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? EmployeeDepartment { get; set; }
    public string? EmployeeEmail { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public LeaveType Type { get; set; }
    public LeaveStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ReviewerNote { get; set; }
}
