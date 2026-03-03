using Commerce.Application.Database;
using Commerce.Application.Exceptions;
using Commerce.Application.Models;
using Commerce.Application.Services.Storages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ValidationException = Commerce.Application.Exceptions.ValidationException;

namespace Commerce.Application.Services.ProductImages;

public class ProductImageService(
    IStorageService storageService,
    AppDbContext dbContext,
    ILogger<ProductImageService> logger) : IProductImageService
{
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB
    private const int MaxImagesPerProduct = 5;

    public async Task<ProductImage> UploadImageAsync(
        Guid productId,
        Stream fileStream,
        string fileName,
        string contentType)
    {
        // ── Validation ────────────────────────────────────────────────────────
        if (fileStream.Length == 0)
            throw new ValidationException("No file uploaded.", "FILE_MISSING");

        if (fileStream.Length > MaxFileSizeBytes)
            throw new ValidationException("File exceeds 5MB limit.", "FILE_TOO_LARGE");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new ValidationException("Only JPG, PNG, and WEBP files are allowed.", "INVALID_FILE_TYPE");

        // ── Product check ─────────────────────────────────────────────────────
        var product = await dbContext.Products
                          .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted)
                      ?? throw new NotFoundException("Product not found.", "PRODUCT_NOT_FOUND");


        // ── Image count check ─────────────────────────────────────────────────
        var existingCount = await dbContext.ProductImages
            .CountAsync(pi => pi.ProductId == productId);

        if (existingCount >= MaxImagesPerProduct)
            throw new ConflictException($"Maximum {MaxImagesPerProduct} images per product.", "MAX_IMAGES_REACHED");

        // ── Image duplication check  ─────────────────────────────────────────────────
        var contentHash = await FileHasher.ComputeSha256Async(fileStream);

        var isDuplicate = await dbContext.ProductImages
            .AnyAsync(pi => pi.ProductId == productId && pi.ContentHash == contentHash);

        if (isDuplicate)
            throw new ConflictException("This image has already been uploaded for this product.", "DUPLICATE_IMAGE");

        // ── Upload & persist ──────────────────────────────────────────────────
        var storedFileName = $"{productId}_{Guid.NewGuid()}{extension}";
        string imageUrl;

        try
        {
            imageUrl = await storageService.UploadAsync(
                fileStream,
                storedFileName,
                contentType
            );
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Storage upload failed for product {ProductId}", productId);
            throw new ServerException("Failed to upload image.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            // If this is the first image, make it primary automatically
            var isPrimary = existingCount == 0;
            var displayOrder = existingCount + 1;

            var productImage = new ProductImage
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                ImageUrl = imageUrl,
                IsPrimary = isPrimary,
                DisplayOrder = displayOrder,
                ContentHash = contentHash,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.ProductImages.Add(productImage);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            logger.LogInformation("Image uploaded for product {ProductId}: {ImageUrl}", productId, imageUrl);

            return productImage;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "DB save failed after upload for product {ProductId}", productId);

            // Cleanup of the uploaded file
            try
            {
                await storageService.DeleteAsync(imageUrl);
            }
            catch (InvalidOperationException exception)
            {
                logger.LogWarning(exception, "S3 delete failed for {ImageUrl}; DB record already removed", imageUrl);
            }

            throw new ServerException("Failed to save image record.");
        }
    }

    public async Task<ProductImage> GetAsync(Guid productId, Guid imageId)
    {
        var productImage = await dbContext.ProductImages
                               .FirstOrDefaultAsync(pi => pi.Id == imageId && pi.ProductId == productId)
                           ?? throw new NotFoundException("Image not found.", "IMAGE_NOT_FOUND");

        return productImage;
    }

    public async Task DeleteAsync(Guid productId, Guid imageId)
    {
        var image = await dbContext.ProductImages
                        .FirstOrDefaultAsync(pi => pi.Id == imageId && pi.ProductId == productId)
                    ?? throw new NotFoundException("Image not found.", "IMAGE_NOT_FOUND");

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            dbContext.ProductImages.Remove(image);
            await dbContext.SaveChangesAsync();

            // If deleted image was primary, promote next image
            if (image.IsPrimary)
            {
                var nextImage = await dbContext.ProductImages
                    .Where(pi => pi.ProductId == productId)
                    .OrderBy(pi => pi.DisplayOrder)
                    .FirstOrDefaultAsync();

                if (nextImage is not null)
                {
                    nextImage.IsPrimary = true;
                    await dbContext.SaveChangesAsync();
                }
            }

            await transaction.CommitAsync();

            // Delete from S3 after DB commit
            try
            {
                await storageService.DeleteAsync(image.ImageUrl);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "S3 delete failed for {ImageUrl}; DB record already removed", image.ImageUrl);
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Failed to delete image {ImageId}", imageId);
            throw new ServerException("Failed to delete image.");
        }
    }

    public async Task SetPrimaryAsync(Guid productId, Guid imageId)
    {
        var images = await dbContext.ProductImages
            .Where(pi => pi.ProductId == productId)
            .ToListAsync();

        var target = images.FirstOrDefault(pi => pi.Id == imageId)
                     ?? throw new NotFoundException("Image not found.", "IMAGE_NOT_FOUND");

        foreach (var img in images)
            img.IsPrimary = img.Id == imageId;

        await dbContext.SaveChangesAsync();
    }
}