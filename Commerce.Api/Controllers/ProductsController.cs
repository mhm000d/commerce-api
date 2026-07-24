using Asp.Versioning;
using Commerce.Api.Mappings;
using Commerce.Application.Models;
using Commerce.Application.Services.Products;
using Commerce.Contracts.Common;
using Commerce.Contracts.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
public class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet(ApiEndpoints.Products.Get)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> Get([FromRoute] string identifier, CancellationToken ct)
    {
        var product = Guid.TryParse(identifier, out var id)
            ? await productService.GetAsync(id, ct)
            : await productService.GetBySlugAsync(identifier, ct);

        return Ok(product.ToResponse());
    }

    [HttpGet(ApiEndpoints.Products.GetAll)]
    [ProducesResponseType(typeof(PagedResponse<ProductsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<ProductsResponse>>> GetAll(
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
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await productService.DeleteAsync(id, ct);
        return NoContent();
    }
}
