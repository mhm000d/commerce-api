using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Commerce.Application.Services.Storages;

public class StorageService(
    IAmazonS3 s3Client,
    IConfiguration config,
    ILogger<StorageService> logger) : IStorageService
{
    private readonly string _bucketName = config["FileUpload:S3:BucketName"]
                                          ?? throw new InvalidOperationException("S3 bucket name is not configured.");

    private readonly string _region = config["FileUpload:S3:Region"]
                                      ?? throw new InvalidOperationException("S3 region name is not configured.");

    private readonly string? _cdnDomain = config["FileUpload:Cdn:Domain"];

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType)
    {
        var key = $"products/{fileName}";

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = fileStream,
            ContentType = contentType,
            // Bucket is private — CloudFront reaches it via Origin Access Control.
            Metadata =
            {
                ["x-amz-meta-original-Name"] = fileName,
                ["x-amz-meta-extension"] = Path.GetExtension(fileName),
                ["x-amz-meta-uploaded-at"] = DateTime.UtcNow.ToString("O")
            }
        };

        try
        {
            await s3Client.PutObjectAsync(request);
            logger.LogInformation("File uploaded to S3: {Key}", key);

            // Return the CDN URL, and store it in the database for later access.
            return BuildPublicUrl(key);
        }
        catch (AmazonS3Exception ex)
        {
            logger.LogError(ex, "S3 upload failed for key {Key}. ErrorCode: {Code}, Status: {Status}",
                key, ex.ErrorCode, ex.StatusCode);
            throw new InvalidOperationException("Failed to upload file to storage", ex);
        }
    }

    public async Task DeleteAsync(string fileUrl)
    {
        var key = ExtractKeyFromUrl(fileUrl);

        var request = new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        };

        try
        {
            await s3Client.DeleteObjectAsync(request);
            logger.LogInformation("File deleted from S3: {Key}", key);
        }
        catch (AmazonS3Exception ex)
        {
            logger.LogError(ex, "S3 delete failed for key {Key}", key);
            throw new InvalidOperationException("Failed to delete file from storage.", ex);
        }
    }

    public async Task<bool> ExistsAsync(string fileUrl)
    {
        var key = ExtractKeyFromUrl(fileUrl);

        try
        {
            await s3Client.GetObjectMetadataAsync(_bucketName, key);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private string BuildPublicUrl(string key)
        => string.IsNullOrWhiteSpace(_cdnDomain)
            ? $"https://{_bucketName}.s3.{_region}.amazonaws.com/{key}"
            : $"https://{_cdnDomain}/{key}";

    private string ExtractKeyFromUrl(string fileUrl)
    {
        // Works for both hosts — only the object key (path) matters, not the domain.
        // e.g. https://d2pbugge3n29nf.cloudfront.net/products/file.jpg → products/file.jpg
        var uri = new Uri(fileUrl);
        return uri.AbsolutePath.TrimStart('/');
    }
}