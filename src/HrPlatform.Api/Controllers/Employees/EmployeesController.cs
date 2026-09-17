using HrPlatform.Api.Services.Employees;
using HrPlatform.Contracts.Dtos.Common;
using HrPlatform.Contracts.Dtos.Employees;
using Microsoft.AspNetCore.Mvc;

namespace HrPlatform.Api.Controllers.Employees;

// thin proxy over the third-party HR system so the UI never talks to dummyjson directly
[ApiController]
[Route("api/employees")]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeClient _employeeClient;

    public EmployeesController(IEmployeeClient employeeClient)
    {
        _employeeClient = employeeClient;
    }

    [HttpGet]
    [ProducesResponseType(typeof(EmployeeListResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmployeeListResultDto>> List(
        [FromQuery] string? search,
        [FromQuery] int limit = 10,
        [FromQuery] int skip = 0,
        CancellationToken ct = default)
    {
        limit = limit is < 1 or > 100 ? 10 : limit;
        skip = skip < 0 ? 0 : skip;

        var result = await _employeeClient.ListAsync(limit, skip, search, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> GetById(int id, CancellationToken ct)
    {
        var employee = await _employeeClient.GetByIdAsync(id, ct);
        if (employee is null)
        {
            return NotFound(new ErrorResponseDto { Error = $"Employee {id} was not found." });
        }

        return Ok(employee);
    }
}
