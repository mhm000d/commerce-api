using Commerce.Api.Mappings;
using Commerce.Application.Models;
using Commerce.Application.Services.Products;
using Commerce.Contracts.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
public class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet(ApiEndpoints.Products.Get)]
    public async Task<IActionResult> Get([FromRoute] Guid id)
    {
        var product = await productService.GetAsync(id);
        return Ok(product.ToResponse());
    }

    [HttpGet(ApiEndpoints.Products.GetAll)]
    public async Task<IActionResult> GetAll()
    {
        var products = await productService.GetAllAsync();
        return Ok(products.ToResponse());
    }

    [HttpPost(ApiEndpoints.Admin.PostProduct)]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<ProductResponse>> Post([FromBody] ProductRequest request)
    {
        var product = await productService.CreateAsync(request.ToDomain());

        return CreatedAtAction(
            nameof(Get),
            new { id = product.Id },
            product.ToResponse()
        );
    }

    [HttpPut(ApiEndpoints.Admin.PutProduct)]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<ProductResponse>> Put(
        [FromRoute] Guid id,
        [FromBody] ProductRequest request)
    {
        var product = await productService.UpdateAsync(id, request.ToDomain());
        return Ok(product.ToResponse());
    }

    [HttpDelete(ApiEndpoints.Admin.DeleteProduct)]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Delete(Guid id)
    {
        await productService.DeleteAsync(id);
        return NoContent();
    }
}
