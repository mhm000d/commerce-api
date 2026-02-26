using Commerce.Application.Models;

namespace Commerce.Application.Features.Products;

public interface IProductService
{
    Task<Product?> GetAsync(Guid id);
    Task<IEnumerable<Product>> GetAllAsync();
}
