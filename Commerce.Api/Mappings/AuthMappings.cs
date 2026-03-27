using Commerce.Application.Services.Auth;
using Commerce.Contracts.Auth;

namespace Commerce.Api.Mappings;

public static class AuthMappings
{
    public static AuthResponse ToResponse(this AuthResult result) => new(
        AccessToken: result.AccessToken,
        RefreshToken: result.RawRefreshToken,
        AccessTokenExpiresAt: result.AccessTokenExpiresAt,
        RefreshTokenExpiresAt: result.RefreshTokenExpiresAt,
        User: new UserResponse(
            result.User.Id,
            result.User.Name,
            result.User.Email,
            result.User.Role
        )
    );
}