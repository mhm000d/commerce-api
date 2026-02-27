namespace Commerce.Application.Models;

public class ProductImage
{
    public required Guid Id { get; set; }
    public required string ImageUrl { get; set; }
    public required bool IsPrimary { get; set; }
    public required int DisplayOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    
    // ── Navigation Properties ─────────────────────────────────────────────────
    public Guid ProductId { get; set; }
    public Product Product { get; private set; } = default!;
}