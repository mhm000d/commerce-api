namespace Commerce.Application.Services.Auth;

public record AuthResult(
    string AccessToken,
    string RawRefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    DateTimeOffset AccessTokenExpiresAt,
    UserDto User
);

public record UserDto(
    Guid Id,
    string Name,
    string Email,
    string Role
);