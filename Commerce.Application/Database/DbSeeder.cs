using Commerce.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Application.Database;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (await context.Products.AnyAsync())
            return;

        context.Products.AddRange(
            new Product
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Dell XPS 13",
                Description = "Premium ultrabook laptop",
                Price = 1200m,
                StockQuantity = 10,
                // Version = [0],
                Category = Category.Laptops,
                Specifications =
                [
                    new("CPU", "Intel Core i7"),
                    new("RAM", "16GB"),
                    new("Storage", "512GB SSD")
                ],
                AverageRating = 4.5m,
                RatingCount = 120,
                IsDeleted = false,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new Product
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Samsung Galaxy S23",
                Description = "Flagship Android smartphone",
                Price = 900m,
                StockQuantity = 25,
                // Version = [0],
                Category = Category.Games,
                Specifications =
                [
                    new("Display", "6.1 inch AMOLED"),
                    new("Storage", "256GB"),
                    new("Camera", "50MP Triple Camera")
                ],
                AverageRating = 4.7m,
                RatingCount = 250,
                IsDeleted = false,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new Product
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "PlayStation 5",
                Description = "Next-gen gaming console",
                Price = 500m,
                StockQuantity = 15,
                // Version = [0],
                Category = Category.Games,
                Specifications =
                [
                    new("Storage", "825GB SSD"),
                    new("Resolution", "Up to 4K"),
                    new("Controller", "DualSense Wireless")
                ],
                AverageRating = 4.8m,
                RatingCount = 500,
                IsDeleted = false,
                CreatedAt = DateTimeOffset.UtcNow
            }
        );

        await context.SaveChangesAsync();
    }
}