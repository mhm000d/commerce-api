using Commerce.Application.Database;
using Commerce.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Application.Services.Products;

public class ProductService(AppDbContext context) : IProductService
{
    public async Task<Product?> GetAsync(Guid id)
    {
        return await context.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        return await context.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .ToListAsync();
    }
}