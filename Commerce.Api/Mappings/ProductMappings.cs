using Commerce.Application.Models;
using Commerce.Contracts.Products;

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
            (Contracts.Products.Category)product.Category,
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
}