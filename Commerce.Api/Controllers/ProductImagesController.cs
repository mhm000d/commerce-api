using Commerce.Api.Mappings;
using Commerce.Application.Models;
using Commerce.Application.Services.ProductImages;
using Commerce.Contracts.ProductImages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
public class ProductImagesController(
    IProductImageService productImageService) : ControllerBase
{
    [HttpPost(ApiEndpoints.Admin.PostImage)]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ProductImageResponse>> PostImage(
        Guid productId,
        IFormFile image)
    {
        var productImage = await productImageService.UploadImageAsync(
            productId,
            image.OpenReadStream(),
            image.FileName,
            image.ContentType
        );

        return CreatedAtAction(
            nameof(GetImage),
            new { productId, imageId = productImage.Id },
            productImage.ToResponse()
        );
    }

    [HttpGet(ApiEndpoints.ProductImages.GetImage)]
    public async Task<ActionResult<ProductImageResponse>> GetImage(Guid productId, Guid imageId)
    {
        var productImage = await productImageService.GetAsync(productId, imageId);
        return productImage.ToResponse();
    }

    [HttpDelete(ApiEndpoints.Admin.DeleteImage)]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> DeleteImage(Guid productId, Guid imageId)
    {
        await productImageService.DeleteAsync(productId, imageId);
        return NoContent();
    }

    [HttpPut(ApiEndpoints.Admin.SetPrimary)]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> SetPrimaryImage(Guid productId, Guid imageId)
    {
        await productImageService.SetPrimaryAsync(productId, imageId);
        return NoContent();
    }
}
