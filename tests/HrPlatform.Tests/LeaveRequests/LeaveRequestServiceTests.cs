using HrPlatform.Api.Data.LeaveRequests;
using HrPlatform.Api.Models.LeaveRequests;
using HrPlatform.Api.Services.LeaveRequests;
using HrPlatform.Contracts.Dtos.Employees;
using HrPlatform.Contracts.Enums.LeaveRequests;
using HrPlatform.Contracts.Dtos.LeaveRequests;
using HrPlatform.Tests.Employees;
using Microsoft.EntityFrameworkCore;

namespace HrPlatform.Tests.LeaveRequests;

public class LeaveRequestServiceTests
{
    private static LeaveRequestsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LeaveRequestsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LeaveRequestsDbContext(options);
    }

    private static readonly EmployeeDto Employee = new()
    {
        Id = 1,
        FirstName = "Ada",
        LastName = "Lovelace",
        Email = "ada@company.com",
        Department = "Engineering",
        Title = "Engineer"
    };

    [Fact]
    public async Task CreateAsync_ThrowsWhenEndDateBeforeStartDate()
    {
        var db = CreateDb();
        var service = new LeaveRequestService(db, new FakeEmployeeClient(Employee));

        var dto = new CreateLeaveRequestDto
        {
            EmployeeId = Employee.Id,
            StartDate = new DateOnly(2026, 1, 10),
            EndDate = new DateOnly(2026, 1, 5),
            Type = LeaveType.Vacation
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenEmployeeNotFound()
    {
        var db = CreateDb();
        var service = new LeaveRequestService(db, new FakeEmployeeClient());

        var dto = new CreateLeaveRequestDto
        {
            EmployeeId = 999,
            StartDate = new DateOnly(2026, 1, 5),
            EndDate = new DateOnly(2026, 1, 10),
            Type = LeaveType.Vacation
        };

        await Assert.ThrowsAsync<EmployeeNotFoundException>(() => service.CreateAsync(dto, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_PersistsAsPendingWithEmployeeDetails()
    {
        var db = CreateDb();
        var service = new LeaveRequestService(db, new FakeEmployeeClient(Employee));

        var dto = new CreateLeaveRequestDto
        {
            EmployeeId = Employee.Id,
            StartDate = new DateOnly(2026, 1, 5),
            EndDate = new DateOnly(2026, 1, 10),
            Type = LeaveType.Sick
        };

        var result = await service.CreateAsync(dto, CancellationToken.None);

        Assert.Equal(LeaveStatus.Pending, result.Status);
        Assert.Equal(Employee.FullName, result.EmployeeName);
        Assert.Single(db.LeaveRequests);
    }

    [Fact]
    public async Task UpdateStatusAsync_ThrowsWhenRequestNotFound()
    {
        var db = CreateDb();
        var service = new LeaveRequestService(db, new FakeEmployeeClient(Employee));

        await Assert.ThrowsAsync<LeaveRequestNotFoundException>(
            () => service.UpdateStatusAsync(1, new UpdateLeaveStatusDto { Status = LeaveStatus.Approved }, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateStatusAsync_ThrowsOnInvalidTransition()
    {
        var db = CreateDb();
        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = Employee.Id,
            StartDate = new DateOnly(2026, 1, 5),
            EndDate = new DateOnly(2026, 1, 10),
            Type = LeaveType.Vacation,
            Status = LeaveStatus.Approved,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new LeaveRequestService(db, new FakeEmployeeClient(Employee));

        await Assert.ThrowsAsync<InvalidStatusTransitionException>(
            () => service.UpdateStatusAsync(1, new UpdateLeaveStatusDto { Status = LeaveStatus.Rejected }, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateStatusAsync_ApprovesPendingRequestAndStoresReviewerNote()
    {
        var db = CreateDb();
        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = Employee.Id,
            StartDate = new DateOnly(2026, 1, 5),
            EndDate = new DateOnly(2026, 1, 10),
            Type = LeaveType.Vacation,
            Status = LeaveStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new LeaveRequestService(db, new FakeEmployeeClient(Employee));

        var result = await service.UpdateStatusAsync(
            1, new UpdateLeaveStatusDto { Status = LeaveStatus.Approved, ReviewerNote = "Enjoy" }, CancellationToken.None);

        Assert.Equal(LeaveStatus.Approved, result.Status);
        Assert.Equal("Enjoy", result.ReviewerNote);
    }

    [Fact]
    public async Task DeleteAsync_ThrowsWhenRequestNotFound()
    {
        var db = CreateDb();
        var service = new LeaveRequestService(db, new FakeEmployeeClient(Employee));

        await Assert.ThrowsAsync<LeaveRequestNotFoundException>(() => service.DeleteAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_ThrowsWhenRequestIsNotPending()
    {
        var db = CreateDb();
        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = Employee.Id,
            StartDate = new DateOnly(2026, 1, 5),
            EndDate = new DateOnly(2026, 1, 10),
            Type = LeaveType.Vacation,
            Status = LeaveStatus.Approved,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new LeaveRequestService(db, new FakeEmployeeClient(Employee));

        await Assert.ThrowsAsync<LeaveRequestNotDeletableException>(() => service.DeleteAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_RemovesPendingRequest()
    {
        var db = CreateDb();
        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = Employee.Id,
            StartDate = new DateOnly(2026, 1, 5),
            EndDate = new DateOnly(2026, 1, 10),
            Type = LeaveType.Vacation,
            Status = LeaveStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new LeaveRequestService(db, new FakeEmployeeClient(Employee));

        await service.DeleteAsync(1, CancellationToken.None);

        Assert.Empty(db.LeaveRequests);
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsWhenNotFound()
    {
        var db = CreateDb();
        var service = new LeaveRequestService(db, new FakeEmployeeClient(Employee));

        await Assert.ThrowsAsync<LeaveRequestNotFoundException>(() => service.GetByIdAsync(1, CancellationToken.None));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public async Task ListAsync_ClampsPageToAtLeastOne(int requestedPage, int expectedPage)
    {
        var db = CreateDb();
        var service = new LeaveRequestService(db, new FakeEmployeeClient(Employee));

        var result = await service.ListAsync(null, null, requestedPage, 20, CancellationToken.None);

        Assert.Equal(expectedPage, result.Page);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(101, 20)]
    [InlineData(-1, 20)]
    [InlineData(50, 50)]
    public async Task ListAsync_ClampsPageSizeToDefaultWhenOutOfRange(int requestedPageSize, int expectedPageSize)
    {
        var db = CreateDb();
        var service = new LeaveRequestService(db, new FakeEmployeeClient(Employee));

        var result = await service.ListAsync(null, null, 1, requestedPageSize, CancellationToken.None);

        Assert.Equal(expectedPageSize, result.PageSize);
    }

    [Fact]
    public async Task ListAsync_FiltersByStatusAndEmployeeId()
    {
        var db = CreateDb();
        db.LeaveRequests.AddRange(
            new LeaveRequest { EmployeeId = 1, StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 2), Type = LeaveType.Vacation, Status = LeaveStatus.Pending, CreatedAt = DateTime.UtcNow },
            new LeaveRequest { EmployeeId = 1, StartDate = new DateOnly(2026, 1, 3), EndDate = new DateOnly(2026, 1, 4), Type = LeaveType.Vacation, Status = LeaveStatus.Approved, CreatedAt = DateTime.UtcNow },
            new LeaveRequest { EmployeeId = 2, StartDate = new DateOnly(2026, 1, 5), EndDate = new DateOnly(2026, 1, 6), Type = LeaveType.Vacation, Status = LeaveStatus.Pending, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var service = new LeaveRequestService(db, new FakeEmployeeClient(Employee));

        var result = await service.ListAsync(LeaveStatus.Pending, 1, 1, 20, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(1, item.EmployeeId);
        Assert.Equal(LeaveStatus.Pending, item.Status);
    }
}
