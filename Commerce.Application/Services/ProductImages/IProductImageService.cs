using Commerce.Application.Models;

namespace Commerce.Application.Services.ProductImages;

public interface IProductImageService
{
    Task<ProductImage> UploadImageAsync(Guid productId, Stream image, string imageFileName, string contentType);
    Task<ProductImage> GetAsync(Guid productId, Guid imageId);
    Task DeleteAsync(Guid productId, Guid imageId);
    Task SetPrimaryAsync(Guid productId, Guid imageId);
}