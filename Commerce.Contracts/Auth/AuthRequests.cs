namespace Commerce.Contracts.Auth;

public record RegisterRequest(
    string Name,
    string Email,
    string Password,
    string? Phone = null);

public record LoginRequest(
    string Email,
    string Password);

public record RefreshRequest(
    string RefreshToken);
    
public record LogoutRequest(
    string RefreshToken);
    
public record ForgotPasswordRequest(
    string Email);
    
public record ResetPasswordRequest(
    string Token,
    string NewPassword);