using System.ComponentModel.DataAnnotations;
using HrPlatform.Contracts.Enums.LeaveRequests;

namespace HrPlatform.Contracts.Dtos.LeaveRequests;

public class CreateLeaveRequestDto
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "EmployeeId must be a positive integer.")]
    public int EmployeeId { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

    [Required]
    public LeaveType Type { get; set; }
}
