using System.Security.Claims;
using Commerce.Api.Mappings;
using Commerce.Application.Services.Ratings;
using Commerce.Contracts.Ratings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
public class RatingsController(IRatingService ratingService) : ControllerBase
{
    [Authorize]
    [HttpPost(ApiEndpoints.Ratings.PostRating)]
    public async Task<IActionResult> Post(
        Guid productId,
        [FromBody] RatingRequest request,
        CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        
        var result = await ratingService.CreateRatingAsync(
            productId, userId, request.Score, request.Comment, ct);

        return StatusCode(201, result.ToResponse());
    }
    
    [Authorize]
    [HttpPut(ApiEndpoints.Ratings.PutRating)]
    public async Task<IActionResult> Put(
        Guid id,
        [FromBody] RatingRequest request,
        CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        
        var result = await ratingService.UpdateRatingAsync(
            id, userId, request.Score, request.Comment, ct);

        return Ok(result.ToResponse());
    }
    
    [Authorize]
    [HttpDelete(ApiEndpoints.Ratings.DeleteRating)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        
        await ratingService.DeleteRatingAsync(id, userId, ct);
        
        return NoContent();
    }
    
    [HttpGet(ApiEndpoints.Ratings.GetRatings)]
    public async Task<IActionResult> GetRatings(
        Guid productId,
        CancellationToken ct)
    {
        var ratings = await ratingService.GetRatingsAsync(productId, ct);

        var response = ratings.Select(r => r.ToResponse());

        return Ok(response.ToList());
    }
}