using Commerce.Application.Exceptions;
using Commerce.Application.Models;
using Commerce.Application.Services.Products;
using Commerce.Contracts.Common;
using Commerce.Contracts.ProductImages;
using Commerce.Contracts.Products;
using Category = Commerce.Application.Models.Category;
using ProductSpecification = Commerce.Application.Models.ProductSpecification;

namespace Commerce.Api.Mappings;

public static class ProductMappings
{
    public static bool TryToCatalogQuery(
        this ProductCatalogRequest request,
        out ProductCatalogQuery query,
        out object? error)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        Category? category = null;
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            if (!Enum.TryParse<Category>(request.Category, ignoreCase: true, out var categoryValue))
            {
                query = default!;
                error = new
                {
                    error = $"'{request.Category}' is not a valid product category.",
                    code = "CATEGORY_NOT_FOUND",
                };
                return false;
            }

            category = categoryValue;
        }

        if (!TryParseSortBy(request.SortBy, out var sortBy))
        {
            query = default!;
            error = new
            {
                error = $"'{request.SortBy}' is not a valid product sort option.",
                code = "INVALID_PRODUCT_SORT",
            };
            return false;
        }

        query = new ProductCatalogQuery(
            page,
            pageSize,
            category,
            request.Search,
            sortBy);
        error = null;
        return true;
    }

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
                    .OrderBy(i => i.IsPrimary ? 0 : 1) // Primary first
                    .ThenBy(i => i.DisplayOrder)
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

    public static PagedResponse<ProductsResponse> ToPagedResponse(
        this IEnumerable<Product> products, int page, int pageSize, int totalCount)
    {
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResponse<ProductsResponse>(
            Data: products.ToResponse().ToList(),
            Pagination: new PaginationMeta(
                Page: page,
                PageSize: pageSize,
                TotalItems: totalCount,
                TotalPages: totalPages,
                HasNext: page < totalPages,
                HasPrevious: page > 1));
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
            .ToList() ?? throw new ValidationException($"Invalid specification: {req.Specifications}",
            "SPECIFIC_CANT_BE_EMPTY");

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

    private static bool TryParseSortBy(string? sortBy, out ProductSortBy parsed)
    {
        parsed = ProductSortBy.Newest;

        if (string.IsNullOrWhiteSpace(sortBy))
            return true;

        var normalized = sortBy.Trim()
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();

        switch (normalized)
        {
            case "newest":
            case "createddesc":
            case "createdatdesc":
                parsed = ProductSortBy.Newest;
                return true;
            case "price":
            case "priceasc":
                parsed = ProductSortBy.PriceAsc;
                return true;
            case "pricedesc":
                parsed = ProductSortBy.PriceDesc;
                return true;
            case "rating":
            case "ratingdesc":
                parsed = ProductSortBy.RatingDesc;
                return true;
            case "ratingasc":
                parsed = ProductSortBy.RatingAsc;
                return true;
            case "name":
            case "nameasc":
                parsed = ProductSortBy.NameAsc;
                return true;
            case "namedesc":
                parsed = ProductSortBy.NameDesc;
                return true;
            default:
                return false;
        }
    }
}
