using Commerce.Application.Exceptions;
using Commerce.Application.Models;
using Commerce.Contracts.ProductImages;
using Commerce.Contracts.Products;
using Category = Commerce.Application.Models.Category;
using ProductSpecification = Commerce.Application.Models.ProductSpecification;

namespace Commerce.Api.Mappings;

public static class ProductMappings
{
    public static ProductResponse ToResponse(this Product product)
    {
        return new ProductResponse(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.StockQuantity,
            product.AverageRating,
            product.RatingCount,
            product.Category.ToString(),
            product.Images
                .Select(i => new ProductImageResponse(
                    i.Id,
                    i.ProductId,
                    i.ImageUrl,
                    i.IsPrimary,
                    i.DisplayOrder)
                ).ToList(),
            product.Specifications
                .Select(s => new Contracts.Products.ProductSpecification(s.Key, s.Value))
                .ToList()
        );
    }

    public static IEnumerable<ProductsResponse> ToResponse(this IEnumerable<Product> products)
    {
        return products.Select(p =>
            new ProductsResponse(
                p.Id,
                p.Name,
                p.Price,
                p.AverageRating,
                p.Images
                    .OrderBy(i => i.DisplayOrder)
                    .Take(2)
                    .Select(i => new ProductImageResponse(
                        i.Id,
                        i.ProductId,
                        i.ImageUrl,
                        i.IsPrimary,
                        i.DisplayOrder)
                    ).ToList()
            )
        );
    }

    // ── Domain Mapping ─────────────────────────────────────────────────
    public static Product ToDomain(this ProductRequest req)
    {
        // Convert category string -> enum safely
        if (!Enum.TryParse<Category>(req.Category, ignoreCase: true, out var category))
        {
            throw new ValidationException($"Invalid category: {req.Category}", "CATEGORY_NOT_FOUND");
        }

        var specs = req.Specifications?
            .Select(s => new ProductSpecification(s.Key, s.Value))
            .ToList() ?? throw new ValidationException($"Invalid specification: {req.Specifications}", "SPECIFIC_CANT_BE_EMPTY");

        return new Product
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Description = req.Description,
            Price = req.Price,
            StockQuantity = req.StockQuantity,
            Category = category,
            Specifications = specs,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}