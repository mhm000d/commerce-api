using System.Security.Claims;
using Commerce.Api.Mappings;
using Commerce.Application.Services.Auth;
using Commerce.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost(ApiEndpoints.Auth.Register)]
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

    [HttpPost(ApiEndpoints.Auth.Login)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request)
    {
        var result = await authService.LoginAsync(request.Email, request.Password);
        return Ok(result.ToResponse());
    }

    // NOTE for a client: replace the stored refresh token immediately after this call.
    // Replaying the old token triggers reuse detection and kills the entire session.
    [HttpPost(ApiEndpoints.Auth.Refresh)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request)
    {
        var result = await authService.RefreshAsync(request.RefreshToken);
        return Ok(result.ToResponse());
    }
    
    [HttpPost(ApiEndpoints.Auth.Logout)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request)
    {
        await authService.LogoutAsync(request.RefreshToken);
        return NoContent();
    }
    
    [Authorize]
    [HttpPost(ApiEndpoints.Auth.LogoutAll)]
    public async Task<IActionResult> LogoutAll()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await authService.LogoutAllAsync(userId);
        return NoContent();
    }
    
    [HttpPost(ApiEndpoints.Auth.ForgotPassword)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request)
    {
        await authService.ForgotPasswordAsync(request.Email);
        return NoContent();
    }
    
    [HttpPost(ApiEndpoints.Auth.ResetPassword)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request)
    {
        await authService.ResetPasswordAsync(request.Token, request.NewPassword);
        return NoContent();
    }
}