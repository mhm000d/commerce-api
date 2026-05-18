namespace Commerce.Contracts.Products;

public record ProductCatalogRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Category { get; set; }
    public string? Search { get; set; }
    public string? SortBy { get; set; }
}
