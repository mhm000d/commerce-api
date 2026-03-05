using Commerce.Api.Mappings;
using Commerce.Application.Services.ProductImages;
using Commerce.Contracts.ProductImages;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
public class ProductImagesController(
    IProductImageService productImageService) : ControllerBase
{
    [HttpPost(ApiEndpoints.Admin.PostImage)]
    public async Task<ActionResult<ProductImageResponse>> PostImage(
        Guid productId,
        [FromForm(Name = "image")] IFormFile image)
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
    public async Task<IActionResult> DeleteImage(Guid productId, Guid imageId)
    {
        await productImageService.DeleteAsync(productId, imageId);
        return NoContent();
    }

    [HttpPut(ApiEndpoints.Admin.SetPrimary)]
    public async Task<IActionResult> SetPrimaryImage(Guid productId, Guid imageId)
    {
        await productImageService.SetPrimaryAsync(productId, imageId);
        return NoContent();
    }
}