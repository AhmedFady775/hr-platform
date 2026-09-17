using HrPlatform.Api.Data.LeaveRequests;
using HrPlatform.Api.Models.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HrPlatform.Api.Services.Auth;

public interface IUserStore
{
    Task<User?> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default);
}

public class UserStore : IUserStore
{
    private readonly LeaveRequestsDbContext _db;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public UserStore(LeaveRequestsDbContext db)
    {
        _db = db;
    }

    public async Task<User?> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower(), ct);

        if (user is null)
        {
            return null;
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Failed ? null : user;
    }
}
