using HrPlatform.Api.Data.LeaveRequests;
using HrPlatform.Api.Middleware;
using HrPlatform.Api.Models.LeaveRequests;
using HrPlatform.Api.Services.Employees;
using HrPlatform.Contracts.Dtos.Common;
using HrPlatform.Contracts.Enums.LeaveRequests;
using HrPlatform.Contracts.Dtos.LeaveRequests;
using Microsoft.EntityFrameworkCore;

namespace HrPlatform.Api.Services.LeaveRequests;

public class LeaveRequestNotFoundException : Exception, IApiException
{
    public int StatusCode => StatusCodes.Status404NotFound;
    public string UserMessage => "Leave request not found.";
}

public class EmployeeNotFoundException : Exception, IApiException
{
    public EmployeeNotFoundException(int employeeId) : base($"Employee {employeeId} was not found in the HR system.")
    {
    }

    public int StatusCode => StatusCodes.Status404NotFound;
    public string UserMessage => Message;
}

public class InvalidStatusTransitionException : Exception, IApiException
{
    public InvalidStatusTransitionException(string message) : base(message)
    {
    }

    public int StatusCode => StatusCodes.Status409Conflict;
    public string UserMessage => Message;
}

public class LeaveRequestNotDeletableException : Exception, IApiException
{
    public LeaveRequestNotDeletableException(string message) : base(message)
    {
    }

    public int StatusCode => StatusCodes.Status400BadRequest;
    public string UserMessage => Message;
}

public interface ILeaveRequestService
{
    Task<PagedResultDto<LeaveRequestDto>> ListAsync(LeaveStatus? status, int? employeeId, int page, int pageSize, CancellationToken ct);
    Task<LeaveRequestDto> GetByIdAsync(int id, CancellationToken ct);
    Task<LeaveRequestDto> CreateAsync(CreateLeaveRequestDto dto, CancellationToken ct);
    Task<LeaveRequestDto> UpdateStatusAsync(int id, UpdateLeaveStatusDto dto, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public class LeaveRequestService : ILeaveRequestService
{
    private readonly LeaveRequestsDbContext _db;
    private readonly IEmployeeClient _employeeClient;

    public LeaveRequestService(LeaveRequestsDbContext db, IEmployeeClient employeeClient)
    {
        _db = db;
        _employeeClient = employeeClient;
    }

    public async Task<PagedResultDto<LeaveRequestDto>> ListAsync(LeaveStatus? status, int? employeeId, int page, int pageSize, CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = _db.LeaveRequests.AsNoTracking().AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        if (employeeId.HasValue)
        {
            query = query.Where(r => r.EmployeeId == employeeId.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var pageItems = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // batch lookup instead of hitting the employee client once per row
        var employeeLookup = await _employeeClient.GetAllAsLookupAsync(ct);

        return new PagedResultDto<LeaveRequestDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = pageItems.Select(r => LeaveRequestMapper.ToDto(r, employeeLookup.GetValueOrDefault(r.EmployeeId))).ToList()
        };
    }

    public async Task<LeaveRequestDto> GetByIdAsync(int id, CancellationToken ct)
    {
        var entity = await _db.LeaveRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct)
                     ?? throw new LeaveRequestNotFoundException();

        var employee = await _employeeClient.GetByIdAsync(entity.EmployeeId, ct);
        return LeaveRequestMapper.ToDto(entity, employee);
    }

    public async Task<LeaveRequestDto> CreateAsync(CreateLeaveRequestDto dto, CancellationToken ct)
    {
        if (dto.EndDate < dto.StartDate)
        {
            throw new ArgumentException("EndDate must be on or after StartDate.");
        }

        var employee = await _employeeClient.GetByIdAsync(dto.EmployeeId, ct)
                        ?? throw new EmployeeNotFoundException(dto.EmployeeId);

        var entity = new LeaveRequest
        {
            EmployeeId = dto.EmployeeId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Type = dto.Type,
            Status = LeaveStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.LeaveRequests.Add(entity);
        await _db.SaveChangesAsync(ct);

        return LeaveRequestMapper.ToDto(entity, employee);
    }

    public async Task<LeaveRequestDto> UpdateStatusAsync(int id, UpdateLeaveStatusDto dto, CancellationToken ct)
    {
        var entity = await _db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == id, ct)
                     ?? throw new LeaveRequestNotFoundException();

        if (!IsValidTransition(entity.Status, dto.Status))
        {
            throw new InvalidStatusTransitionException(
                $"Cannot transition a request from '{entity.Status}' to '{dto.Status}'. Only Pending requests can be Approved or Rejected.");
        }

        entity.Status = dto.Status;
        entity.ReviewerNote = dto.ReviewerNote;
        await _db.SaveChangesAsync(ct);

        var employee = await _employeeClient.GetByIdAsync(entity.EmployeeId, ct);
        return LeaveRequestMapper.ToDto(entity, employee);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await _db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == id, ct)
                     ?? throw new LeaveRequestNotFoundException();

        if (entity.Status != LeaveStatus.Pending)
        {
            throw new LeaveRequestNotDeletableException("Only Pending leave requests can be deleted.");
        }

        _db.LeaveRequests.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    public static bool IsValidTransition(LeaveStatus current, LeaveStatus target)
    {
        return current == LeaveStatus.Pending &&
               (target == LeaveStatus.Approved || target == LeaveStatus.Rejected);
    }
}
