using System.Security.Claims;
using Asp.Versioning;
using Commerce.Api.Mappings;
using Commerce.Application.Services.Auth;
using Commerce.Contracts.Auth;
using Commerce.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost(ApiEndpoints.Auth.Register)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request)
    {
        var result = await authService.RegisterAsync(
            request.Name,
            request.Email,
            request.Password, // raw — service handles hashing
            request.Phone
        );

        return StatusCode(201, result.ToResponse());
    }

    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost(ApiEndpoints.Auth.Login)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request)
    {
        var result = await authService.LoginAsync(request.Email, request.Password);
        return Ok(result.ToResponse());
    }

    // NOTE for a client: replace the stored refresh token immediately after this call.
    // Replaying the old token triggers reuse detection and kills the entire session.
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost(ApiEndpoints.Auth.Refresh)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request)
    {
        var result = await authService.RefreshAsync(request.RefreshToken);
        return Ok(result.ToResponse());
    }
    
    [HttpPost(ApiEndpoints.Auth.Logout)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request)
    {
        await authService.LogoutAsync(request.RefreshToken);
        return NoContent();
    }
    
    [Authorize]
    [HttpPost(ApiEndpoints.Auth.LogoutAll)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutAll()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await authService.LogoutAllAsync(userId);
        return NoContent();
    }
    
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost(ApiEndpoints.Auth.ForgotPassword)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken ct)
    {
        await authService.ForgotPasswordAsync(request.Email, ct);
        return NoContent();
    }
    
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost(ApiEndpoints.Auth.ResetPassword)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request)
    {
        await authService.ResetPasswordAsync(request.Token, request.NewPassword);
        return NoContent();
    }
}
