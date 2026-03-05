using Commerce.Application.Models;
using Commerce.Contracts.ProductImages;
using Commerce.Contracts.Products;

namespace Commerce.Api.Mappings;

public static class ProductImageMappings
{
    public static ProductImageResponse ToResponse(this ProductImage productImage)
    {
        return new ProductImageResponse(
            productImage.Id,
            productImage.ProductId,
            productImage.ImageUrl,
            productImage.IsPrimary,
            productImage.DisplayOrder);
    }
}