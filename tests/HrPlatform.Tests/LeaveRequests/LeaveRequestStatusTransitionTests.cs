using HrPlatform.Api.Services.LeaveRequests;
using HrPlatform.Contracts.Enums.LeaveRequests;

namespace HrPlatform.Tests.LeaveRequests;

public class LeaveRequestStatusTransitionTests
{
    [Theory]
    [InlineData(LeaveStatus.Pending, LeaveStatus.Approved, true)]
    [InlineData(LeaveStatus.Pending, LeaveStatus.Rejected, true)]
    [InlineData(LeaveStatus.Pending, LeaveStatus.Pending, false)]
    [InlineData(LeaveStatus.Approved, LeaveStatus.Rejected, false)]
    [InlineData(LeaveStatus.Approved, LeaveStatus.Pending, false)]
    [InlineData(LeaveStatus.Rejected, LeaveStatus.Approved, false)]
    [InlineData(LeaveStatus.Rejected, LeaveStatus.Pending, false)]
    public void IsValidTransition_EnforcesPendingOnlyOrigin(LeaveStatus current, LeaveStatus target, bool expected)
    {
        var result = LeaveRequestService.IsValidTransition(current, target);

        Assert.Equal(expected, result);
    }
}
