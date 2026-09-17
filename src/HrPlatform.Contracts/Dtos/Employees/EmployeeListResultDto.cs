namespace HrPlatform.Contracts.Dtos.Employees;

public class EmployeeListResultDto
{
    public List<EmployeeDto> Employees { get; set; } = new();
    public int Total { get; set; }
}
