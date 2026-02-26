using Commerce.Api.Mappings;
using Commerce.Application.Features.Products;
using Commerce.Contracts.Products;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get([FromRoute] Guid id)
    {
        var product = await productService.GetAsync(id);

        if (product == null) return NotFound();

        return Ok(product.ToResponse());
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await productService.GetAllAsync();
        
        return Ok(products.ToResponse());
    }
}