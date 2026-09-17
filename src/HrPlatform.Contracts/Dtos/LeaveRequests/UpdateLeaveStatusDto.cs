using System.ComponentModel.DataAnnotations;
using HrPlatform.Contracts.Enums.LeaveRequests;

namespace HrPlatform.Contracts.Dtos.LeaveRequests;

public class UpdateLeaveStatusDto
{
    [Required]
    public LeaveStatus Status { get; set; }

    [MaxLength(500)]
    public string? ReviewerNote { get; set; }
}
