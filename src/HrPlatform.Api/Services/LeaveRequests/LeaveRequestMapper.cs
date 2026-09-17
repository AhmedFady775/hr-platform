using HrPlatform.Api.Models.LeaveRequests;
using HrPlatform.Contracts.Dtos.Employees;
using HrPlatform.Contracts.Dtos.LeaveRequests;

namespace HrPlatform.Api.Services.LeaveRequests;

internal static class LeaveRequestMapper
{
    public static LeaveRequestDto ToDto(LeaveRequest entity, EmployeeDto? employee) => new()
    {
        Id = entity.Id,
        EmployeeId = entity.EmployeeId,
        EmployeeName = employee?.FullName,
        EmployeeDepartment = employee?.Department,
        EmployeeEmail = employee?.Email,
        StartDate = entity.StartDate,
        EndDate = entity.EndDate,
        Type = entity.Type,
        Status = entity.Status,
        CreatedAt = entity.CreatedAt,
        ReviewerNote = entity.ReviewerNote
    };
}
