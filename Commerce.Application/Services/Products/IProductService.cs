using Commerce.Application.Models;

namespace Commerce.Application.Services.Products;

public interface IProductService
{
    Task<Product?> GetAsync(Guid id);
    Task<IEnumerable<Product>> GetAllAsync();
}
