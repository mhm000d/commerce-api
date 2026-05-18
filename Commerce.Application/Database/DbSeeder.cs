using System.Security.Cryptography;
using System.Text;
using Commerce.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Application.Database;

public static class DbSeeder
{
    private const string DevelopmentAdminEmail = "admin@commerce.local";
    private const string DevelopmentAdminPassword = "Admin123!";

    private static readonly Guid DellXps13Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GalaxyS24Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid PlayStation5Id = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid LgOledTvId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid MxKeysComboId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    public static async Task SeedAsync(
        AppDbContext context,
        bool seedDevelopmentAdmin = false,
        bool resetDemoCatalog = false)
    {
        if (seedDevelopmentAdmin)
            await SeedDevelopmentAdminAsync(context);

        await SeedProductsAsync(context, resetDemoCatalog);
    }

    private static async Task SeedProductsAsync(AppDbContext context, bool resetDemoCatalog)
    {
        if (!resetDemoCatalog && await context.Products.IgnoreQueryFilters().AnyAsync())
            return;

        await using var transaction = await context.Database.BeginTransactionAsync();

        if (resetDemoCatalog)
            await ResetDemoCatalogAsync(context);

        var seededAt = DateTimeOffset.UtcNow;
        context.Products.AddRange(CreateSeedProducts(seededAt));
        context.ProductImages.AddRange(CreateSeedProductImages(seededAt));

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static async Task SeedDevelopmentAdminAsync(AppDbContext context)
    {
        var adminExists = await context.Users
            .AnyAsync(u => u.Email == DevelopmentAdminEmail || u.Role == UserRole.Admin);

        if (adminExists)
            return;

        var admin = User.Create(
            name: "Development Admin",
            email: DevelopmentAdminEmail,
            rawPassword: DevelopmentAdminPassword);

        admin.PromoteToAdmin();
        context.Users.Add(admin);

        await context.SaveChangesAsync();
    }

    private static async Task ResetDemoCatalogAsync(AppDbContext context)
    {
        await context.EmailNotifications.ExecuteDeleteAsync();
        await context.WebhookEvents.ExecuteDeleteAsync();
        await context.Payments.ExecuteDeleteAsync();
        await context.Orders.ExecuteDeleteAsync();
        await context.CartItems.IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.Ratings.IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.ProductImages.IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.Products.IgnoreQueryFilters().ExecuteDeleteAsync();
    }

    private static IEnumerable<Product> CreateSeedProducts(DateTimeOffset createdAt) =>
    [
        CreateProduct(
            DellXps13Id,
            "Dell XPS 13",
            "Compact premium laptop with a bright display, fast SSD storage, and all-day portability.",
            1299m,
            12,
            Category.Laptops,
            4.6m,
            84,
            createdAt,
            new("CPU", "Intel Core Ultra 7"),
            new("RAM", "16GB"),
            new("Storage", "512GB SSD")),

        CreateProduct(
            GalaxyS24Id,
            "Samsung Galaxy S24",
            "Flagship Android phone with an AMOLED display, fast performance, and a versatile camera system.",
            899m,
            24,
            Category.Electronics,
            4.7m,
            132,
            createdAt,
            new("Display", "6.2 inch AMOLED"),
            new("Storage", "256GB"),
            new("Camera", "50MP triple camera")),

        CreateProduct(
            PlayStation5Id,
            "Sony PlayStation 5 Slim",
            "Current-generation gaming console with fast loading, 4K gaming, and a DualSense controller.",
            499m,
            15,
            Category.Games,
            4.8m,
            215,
            createdAt,
            new("Storage", "1TB SSD"),
            new("Resolution", "Up to 4K"),
            new("Controller", "DualSense wireless")),

        CreateProduct(
            LgOledTvId,
            "LG OLED C3 55-inch TV",
            "OLED television with deep contrast, low input lag, and cinema-grade HDR support.",
            1399m,
            8,
            Category.Televisions,
            4.6m,
            97,
            createdAt,
            new("Display", "55 inch OLED"),
            new("Refresh Rate", "120Hz"),
            new("HDR", "Dolby Vision")),

        CreateProduct(
            MxKeysComboId,
            "Logitech MX Keys S Combo",
            "Wireless productivity keyboard and mouse bundle for quiet typing and precise control.",
            199m,
            30,
            Category.Other,
            4.4m,
            61,
            createdAt,
            new("Keyboard", "Backlit low-profile"),
            new("Mouse", "MX Master 3S"),
            new("Connectivity", "Bluetooth and USB receiver"))
    ];

    private static IEnumerable<ProductImage> CreateSeedProductImages(DateTimeOffset createdAt) =>
    [
        CreateProductImage(
            Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"),
            DellXps13Id,
            "https://placehold.co/600x600/png?text=Dell+XPS+13",
            createdAt),

        CreateProductImage(
            Guid.Parse("aaaaaaaa-2222-2222-2222-222222222222"),
            GalaxyS24Id,
            "https://placehold.co/600x600/png?text=Galaxy+S24",
            createdAt),

        CreateProductImage(
            Guid.Parse("aaaaaaaa-3333-3333-3333-333333333333"),
            PlayStation5Id,
            "https://placehold.co/600x600/png?text=PlayStation+5",
            createdAt),

        CreateProductImage(
            Guid.Parse("aaaaaaaa-4444-4444-4444-444444444444"),
            LgOledTvId,
            "https://placehold.co/600x600/png?text=LG+OLED+C3",
            createdAt),

        CreateProductImage(
            Guid.Parse("aaaaaaaa-5555-5555-5555-555555555555"),
            MxKeysComboId,
            "https://placehold.co/600x600/png?text=MX+Keys+S",
            createdAt)
    ];

    private static Product CreateProduct(
        Guid id,
        string name,
        string description,
        decimal price,
        int stockQuantity,
        Category category,
        decimal averageRating,
        int ratingCount,
        DateTimeOffset createdAt,
        params ProductSpecification[] specifications)
    {
        return new Product
        {
            Id = id,
            Name = name,
            Description = description,
            Price = price,
            StockQuantity = stockQuantity,
            Category = category,
            Specifications = specifications.ToList(),
            AverageRating = averageRating,
            RatingCount = ratingCount,
            IsDeleted = false,
            CreatedAt = createdAt
        };
    }

    private static ProductImage CreateProductImage(
        Guid id,
        Guid productId,
        string imageUrl,
        DateTimeOffset createdAt)
    {
        return new ProductImage
        {
            Id = id,
            ProductId = productId,
            ImageUrl = imageUrl,
            IsPrimary = true,
            DisplayOrder = 0,
            ContentHash = CreateContentHash(imageUrl),
            CreatedAt = createdAt
        };
    }

    private static string CreateContentHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
