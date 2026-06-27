using System.Security.Claims;
using Commerce.Api.Mappings;
using Commerce.Contracts.Account;
using Commerce.Application.Services.Account;
using Commerce.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class AccountController(IAccountService accountService) : ControllerBase
{
    [HttpGet(ApiEndpoints.Account.GetProfile)]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var user = await accountService.GetProfileAsync(GetUserId(), ct);
        return Ok(user.ToProfileResponse());
    }

    [HttpPut(ApiEndpoints.Account.UpdateProfile)]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await accountService.UpdateProfileAsync(GetUserId(), request.Name, request.Phone, ct);
        return Ok(user.ToProfileResponse());
    }

    [HttpPost(ApiEndpoints.Account.ChangePassword)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await accountService.ChangePasswordAsync(GetUserId(), request.CurrentPassword, request.NewPassword, ct);
        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}