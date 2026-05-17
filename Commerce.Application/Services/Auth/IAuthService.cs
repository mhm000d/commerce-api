using Commerce.Application.Models;

namespace Commerce.Application.Services.Auth;

public interface IAuthService
{
    /// <summary>
    /// Registers a new user and generates an authentication token pair upon successful registration.
    /// </summary>
    Task<AuthResult> RegisterAsync(string name, string email, string rawPassword, string? phone);
    
    /// <summary>
    /// Authenticates a user and returns a new token pair.
    /// </summary>
    Task<AuthResult> LoginAsync(string email, string rawPassword);
    
    /// <summary>
    /// Refreshes an expired access token using a valid refresh token.
    /// </summary>
    Task<AuthResult> RefreshAsync(string rawRefreshToken);
    
    /// <summary>
    /// Logs out the current session by revoking the provided refresh token.
    /// </summary>
    Task LogoutAsync(string rawRefreshToken);
    
    /// <summary>
    /// Logs out the user from all devices by revoking all active sessions.
    /// </summary>
    Task LogoutAllAsync(Guid userId);
    
    /// <summary>
    /// Initiates a password reset process for the given email.
    /// </summary>
    Task ForgotPasswordAsync(string email, CancellationToken ct = default);
    
    /// <summary>
    /// Resets the user's password using a valid reset token.
    /// </summary>
    Task ResetPasswordAsync(string rawToken, string newRawPassword);
}