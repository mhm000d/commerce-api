using Commerce.Application.Database;
using Commerce.Application.Exceptions;
using Commerce.Application.Models;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Commerce.Application.Services.Products;

public class ProductService(
    AppDbContext dbContext,
    IValidator<Product> productValidator,
    ILogger<ProductService> logger) : IProductService
{
    public async Task<Product> GetAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext.Products
                   .AsNoTracking()
                   .Include(p => p.Images)
                   .FirstOrDefaultAsync(p => p.Id == id, ct)
               ?? throw new NotFoundException("Product not found.", "PRODUCT_NOT_FOUND");
    }

    public async Task<(IEnumerable<Product> Products, int TotalCount)> GetAllAsync(
        ProductCatalogQuery catalogQuery,
        CancellationToken ct = default)
    {
        var page = Math.Max(catalogQuery.Page, 1);
        var pageSize = Math.Clamp(catalogQuery.PageSize, 1, 100);

        var query = dbContext.Products
            .AsNoTracking();

        if (catalogQuery.Category.HasValue)
            query = query.Where(p => p.Category == catalogQuery.Category.Value);

        foreach (var term in GetSearchTerms(catalogQuery.Search))
        {
            var pattern = $"%{EscapeLikePattern(term)}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, pattern, "\\") ||
                EF.Functions.ILike(p.Description, pattern, "\\"));
        }

        var totalCount = await query.CountAsync(ct);

        query = catalogQuery.SortBy switch
        {
            ProductSortBy.PriceAsc => query
                .OrderBy(p => p.Price)
                .ThenByDescending(p => p.CreatedAt),

            ProductSortBy.PriceDesc => query
                .OrderByDescending(p => p.Price)
                .ThenByDescending(p => p.CreatedAt),

            ProductSortBy.RatingDesc => query
                .OrderByDescending(p => p.AverageRating ?? 0m)
                .ThenByDescending(p => p.RatingCount)
                .ThenByDescending(p => p.CreatedAt),

            ProductSortBy.RatingAsc => query
                .OrderBy(p => p.AverageRating ?? 0m)
                .ThenByDescending(p => p.CreatedAt),

            ProductSortBy.NameAsc => query
                .OrderBy(p => p.Name)
                .ThenByDescending(p => p.CreatedAt),

            ProductSortBy.NameDesc => query
                .OrderByDescending(p => p.Name)
                .ThenByDescending(p => p.CreatedAt),

            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        var products = await query
            .Include(p => p.Images)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (products, totalCount);
    }

    public async Task<Product> CreateAsync(Product product, CancellationToken ct = default)
    {
        var newProduct = Product.Create(
            product.Name,
            product.Description,
            product.Price,
            product.StockQuantity,
            product.Category);

        newProduct.SetSpecifications(product.Specifications);

        await productValidator.ValidateAndThrowAsync(newProduct, ct);

        dbContext.Products.Add(newProduct);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Product created. ProductId={ProductId}", newProduct.Id);

        return newProduct;
    }

    public async Task<Product> UpdateAsync(Guid id, Product updatedProduct, CancellationToken ct = default)
    {
        var existingProduct = await dbContext.Products
                                  .Include(p => p.Specifications)
                                  .FirstOrDefaultAsync(p => p.Id == id, ct)
                              ?? throw new NotFoundException("Product not found.", "PRODUCT_NOT_FOUND");

        existingProduct.UpdateDetails(
            updatedProduct.Name,
            updatedProduct.Description,
            updatedProduct.Price,
            updatedProduct.StockQuantity,
            updatedProduct.Category
        );
        existingProduct.SetSpecifications(updatedProduct.Specifications);

        await productValidator.ValidateAndThrowAsync(existingProduct, ct);

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Product updated. ProductId={ProductId}", id);

        return existingProduct;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existingProduct = await dbContext.Products
                                  .FirstOrDefaultAsync(p => p.Id == id, ct)
                              ?? throw new NotFoundException("Product not found.", "PRODUCT_NOT_FOUND");

        existingProduct.Delete();
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Product deleted. ProductId={ProductId}", id);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static IEnumerable<string> GetSearchTerms(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return [];

        return search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string EscapeLikePattern(string value) =>
        value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
}
