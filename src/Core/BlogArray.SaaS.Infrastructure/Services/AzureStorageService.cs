using Azure.Storage.Blobs;
using BlogArray.SaaS.Domain.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace BlogArray.SaaS.Infrastructure.Services;

public interface IAzureStorageService
{
    Task<ReturnResult<string>> Upload(IFormFile file, string iconType, bool isCropped);
}

public class AzureStorageService(IConfiguration configuration) : IAzureStorageService
{
    private readonly string _separator = Path.DirectorySeparatorChar.ToString();

    public async Task<ReturnResult<string>> Upload(IFormFile file, string iconType, bool isCropped)
    {
        ReturnResult<string> returnResult = new() { Status = false };

        string guid = Guid.NewGuid().ToString();
        string extension = isCropped ? ".webp" : Path.GetExtension(file.FileName ?? file.Name);
        string dbPath = GetDbPath(iconType, guid, extension);

        returnResult.Result = await ToBlob(file, dbPath);
        returnResult.Status = true;

        return returnResult;
    }

    private async Task<string> ToBlob(IFormFile file, string path)
    {
        using MemoryStream output = new();
        file.CopyTo(output);
        return await UploadToAzure(output, path);
    }

    private async Task<string> UploadToAzure(MemoryStream stream, string path)
    {
        string connectionString = configuration.GetConnectionString("StorageConnectionString");

        BlobServiceClient blobServiceClient = new(connectionString);

        string containerName = configuration.GetConnectionString("StorageContainer");

        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        BlobClient blobClient = containerClient.GetBlobClient(path);

        stream.Position = 0;

        await blobClient.UploadAsync(stream, true);

        return PathToUrl(blobClient.Uri);
    }

    private string PathToUrl(Uri uri)
    {
        return Uri.UnescapeDataString(uri.ToString()).Replace(_separator, "/");
    }

    private static string GetDbPath(string iconType, string guid, string extension)
    {
        string fileName = guid + extension;

        return Path.Combine(iconType.ToLower(), fileName);
    }
}
