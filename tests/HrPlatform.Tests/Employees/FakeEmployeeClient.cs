using HrPlatform.Api.Services.Employees;
using HrPlatform.Contracts.Dtos.Employees;

namespace HrPlatform.Tests.Employees;

public class FakeEmployeeClient : IEmployeeClient
{
    private readonly Dictionary<int, EmployeeDto> _employees;

    public FakeEmployeeClient(params EmployeeDto[] employees)
    {
        _employees = employees.ToDictionary(e => e.Id, e => e);
    }

    public Task<EmployeeDto?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_employees.GetValueOrDefault(id));

    public Task<EmployeeListResultDto> ListAsync(int limit, int skip, string? search, CancellationToken ct = default) =>
        Task.FromResult(new EmployeeListResultDto
        {
            Employees = _employees.Values.Skip(skip).Take(limit).ToList(),
            Total = _employees.Count
        });

    public Task<Dictionary<int, EmployeeDto>> GetAllAsLookupAsync(CancellationToken ct = default) =>
        Task.FromResult(_employees);
}
