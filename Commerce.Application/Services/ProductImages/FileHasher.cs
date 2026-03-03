using System.Security.Cryptography;

namespace Commerce.Application.Services.ProductImages;

public static class FileHasher
{
    /// <summary>
    /// Computes SHA-256 hash of a stream.
    /// Returns the stream position back to 0 so it can still be uploaded.
    /// </summary>
    public static async Task<string> ComputeSha256Async(Stream stream)
    {
        stream.Position = 0;
        var hashBytes = await SHA256.HashDataAsync(stream);
        stream.Position = 0; // Reset so the stream can be read again for upload
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}