using Commerce.Application.Models;

namespace Commerce.Application.Services.Products;

public record ProductCatalogQuery(
    int Page,
    int PageSize,
    Category? Category,
    string? Search,
    ProductSortBy SortBy);
