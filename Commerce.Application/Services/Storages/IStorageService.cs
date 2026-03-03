namespace Commerce.Application.Services.Storages;

public interface IStorageService
{
    Task<string> UploadAsync(Stream fileStream, string fileName, string contentType);
    Task DeleteAsync(string fileUrl);
    Task<bool> ExistsAsync(string fileUrl);
}