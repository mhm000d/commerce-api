using Commerce.Application.Models;

namespace Commerce.Application.Services.Account;

public interface IAccountService
{
    Task<User> GetProfileAsync(Guid userId, CancellationToken ct = default);
    Task<User> UpdateProfileAsync(Guid userId, string name, string? phone, CancellationToken ct = default);
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default);
}