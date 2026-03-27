namespace Commerce.Contracts.Auth;

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    UserResponse User);

public record UserResponse(
    Guid Id,
    string Name,
    string Email,
    string Role);