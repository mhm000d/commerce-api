using Commerce.Application.Models;
using Commerce.Contracts.Account;

namespace Commerce.Api.Mappings;

public static class AccountMappings
{
    public static ProfileResponse ToProfileResponse(this User user) =>
        new(
            user.Id,
            user.Name,
            user.Email,
            user.Phone,
            user.Role.ToString(),
            user.CreatedAt
        );
}