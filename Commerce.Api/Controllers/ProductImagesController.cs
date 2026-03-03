using Commerce.Api.Mappings;
using Commerce.Application.Services.ProductImages;
using Commerce.Contracts.Products;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
[Route("api/products/{productId:guid}/images")]
public class ProductImagesController(
    IProductImageService productImageService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ProductImageResponse>> UploadImage(
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

    [HttpGet("{imageId:guid}")]
    public async Task<ActionResult<ProductImageResponse>> GetImage(Guid productId, Guid imageId)
    {
        var productImage = await productImageService.GetAsync(productId, imageId);
        return productImage.ToResponse();
    }

    [HttpDelete("{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid productId, Guid imageId)
    {
        await productImageService.DeleteAsync(productId, imageId);
        return NoContent();
    }

    [HttpPut("{imageId:guid}/set-primary")]
    public async Task<IActionResult> SetPrimaryImage(Guid productId, Guid imageId)
    {
        await productImageService.SetPrimaryAsync(productId, imageId);
        return NoContent();
    }
}