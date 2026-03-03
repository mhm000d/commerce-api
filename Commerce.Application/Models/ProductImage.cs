namespace Commerce.Application.Models;

public class ProductImage
{
    public required Guid Id { get; set; }
    public required string ImageUrl { get; set; }
    public required bool IsPrimary { get; set; }
    public required int DisplayOrder { get; set; }
    public string ContentHash { get; set; } = null!; // SHA-256 hex string
    public DateTimeOffset CreatedAt { get; set; }
    
    // ── Navigation Properties ─────────────────────────────────────────────────
    public Guid ProductId { get; set; }
    public Product Product { get; private set; } = default!;
}