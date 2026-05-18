using Commerce.Application.Models;

namespace Commerce.Application.Services.Products;

public interface IProductService
{
    Task<Product> GetAsync(Guid id, CancellationToken ct = default);
    Task<(IEnumerable<Product> Products, int TotalCount)> GetAllAsync(
        ProductCatalogQuery query,
        CancellationToken ct = default);

    Task<Product> CreateAsync(Product product, CancellationToken ct = default);
    Task<Product> UpdateAsync(Guid id, Product product, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
