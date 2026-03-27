using Commerce.Application.Models;

namespace Commerce.Application.Services.Auth;

public interface ITokenService
{
    /// <summary>Issues a short-lived JWT access token (15 min).</summary>
    string GenerateAccessToken(User user);

    /// <summary>
    /// Generates a cryptographically secure random refresh token.
    /// Returns the raw value (sent to client) and its hash (stored in DB).
    /// </summary>
    (string rawToken, string tokenHash) GenerateRefreshToken();

    /// <summary>Returns now + configured refresh token lifetime.</summary>
    DateTimeOffset RefreshTokenExpiresAt();
    
    /// <summary>Returns now + configured access token lifetime.</summary>
    DateTimeOffset AccessTokenExpiresAt();
}