using System.Security.Claims;
using Asp.Versioning;
using Commerce.Api.Mappings;
using Commerce.Application.Services.Ratings;
using Commerce.Contracts.Ratings;
using Commerce.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
public class RatingsController(IRatingService ratingService) : ControllerBase
{
    [Authorize]
    [HttpPost(ApiEndpoints.Ratings.PostRating)]
    [ProducesResponseType(typeof(RatingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
    [ProducesResponseType(typeof(RatingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        
        await ratingService.DeleteRatingAsync(id, userId, ct);
        
        return NoContent();
    }
    
    [HttpGet(ApiEndpoints.Ratings.GetRatings)]
    [ProducesResponseType(typeof(PagedResponse<RatingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRatings(
        Guid productId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5,
        [FromQuery] string? sortBy = "newest",
        CancellationToken ct = default)
    {
        var (ratings, totalCount) = await ratingService.GetRatingsAsync(
            productId, page, pageSize, sortBy, ct);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var response = new PagedResponse<RatingResponse>
        (
            Data: ratings.Select(r => r.ToResponse()).ToList(),
            Pagination: new PaginationMeta(
                Page: page,
                PageSize: pageSize,
                TotalItems: totalCount,
                TotalPages: totalPages,
                HasNext: page < totalPages,
                HasPrevious: page > 1)
        );

        return Ok(response);
    }
}