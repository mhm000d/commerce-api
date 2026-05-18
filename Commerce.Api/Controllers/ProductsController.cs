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
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken ct)
    {
        var product = await productService.GetAsync(id, ct);
        return Ok(product.ToResponse());
    }

    [HttpGet(ApiEndpoints.Products.GetAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] ProductCatalogRequest request,
        CancellationToken ct = default)
    {
        if (!request.TryToCatalogQuery(out var query, out var error))
            return BadRequest(error);

        var (products, total) = await productService.GetAllAsync(query, ct);

        return Ok(products.ToPagedResponse(query.Page, query.PageSize, total));
    }

    [HttpPost(ApiEndpoints.Admin.PostProduct)]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<ProductResponse>> Post([FromBody] ProductRequest request, CancellationToken ct)
    {
        var product = await productService.CreateAsync(request.ToDomain(), ct);

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
        [FromBody] ProductRequest request,
        CancellationToken ct)
    {
        var product = await productService.UpdateAsync(id, request.ToDomain(), ct);
        return Ok(product.ToResponse());
    }

    [HttpDelete(ApiEndpoints.Admin.DeleteProduct)]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await productService.DeleteAsync(id, ct);
        return NoContent();
    }
}
