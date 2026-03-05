using Commerce.Application.Database;
using Commerce.Application.Exceptions;
using Commerce.Application.Models;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Application.Services.Products;

public class ProductService(
    AppDbContext dbContext,
    IValidator<Product> productValidator) : IProductService
{
    public async Task<Product> GetAsync(Guid id)
    {
        return await dbContext.Products
                   .AsNoTracking()
                   .Include(p => p.Images)
                   .FirstOrDefaultAsync(p => p.Id == id)
               ?? throw new NotFoundException("Product  not found", "NO_PRODUCT_FOUND");
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        return await dbContext.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .ToListAsync();
    }

    public async Task<Product> CreateAsync(Product product)
    {
        await productValidator.ValidateAndThrowAsync(product);

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        return product;
    }

    public async Task<Product> UpdateAsync(Guid id, Product updatedProduct)
    {
        var existingProduct = await dbContext.Products
                                  // .Include(p => p.Images)
                                  .Include(p => p.Specifications)
                                  .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted)
                              ?? throw new NotFoundException("Product not found", "NO_PRODUCT_FOUND");

        await productValidator.ValidateAndThrowAsync(updatedProduct);

        existingProduct.UpdateDetails(
            updatedProduct.Name,
            updatedProduct.Description,
            updatedProduct.Price,
            updatedProduct.StockQuantity,
            updatedProduct.Category
        );
        existingProduct.SetSpecifications(updatedProduct.Specifications);
        
        await dbContext.SaveChangesAsync();
        return existingProduct;
    }

    public async Task DeleteAsync(Guid id)
    {
        var existingProduct = await dbContext.Products
                                  .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted)
                              ?? throw new NotFoundException("Product not found", "NO_PRODUCT_FOUND");

        existingProduct.Delete();
        await dbContext.SaveChangesAsync();
    }
}