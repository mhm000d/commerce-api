using Commerce.Application.Models;

namespace Commerce.Application.Services.Products;

public interface IProductService
{
    Task<Product> GetAsync(Guid id);
    Task<IEnumerable<Product>> GetAllAsync();
    Task<Product> CreateAsync(Product product);
    Task<Product> UpdateAsync(Guid id, Product product);
    Task DeleteAsync(Guid id);
}
