using Commerce.Application.Database;
using Commerce.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Application.Features.Products;

public class ProductService(AppDbContext context) : IProductService
{
    public Task<Product?> GetAsync(Guid id)
    {
        return context.Products.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        return await context.Products.ToListAsync();
    }
}