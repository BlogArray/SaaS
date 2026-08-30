//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

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
    private static readonly HashSet<string> AllowedIconTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "user-icon",
        "logo",
        "favicon"
    };

    // Only raster image formats are accepted; SVG is deliberately excluded because it can
    // carry scripts and would be served from the storage origin.
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".webp",
        ".ico",
        ".bmp"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/pjpeg",
        "image/gif",
        "image/webp",
        "image/x-icon",
        "image/vnd.microsoft.icon",
        "image/bmp"
    };

    private readonly string _separator = Path.DirectorySeparatorChar.ToString();

    public async Task<ReturnResult<string>> Upload(IFormFile file, string iconType, bool isCropped)
    {
        ReturnResult<string> returnResult = new() { Status = false };

        // The blob path prefix is always derived from a fixed allow-list: values arriving from
        // request parameters can never traverse into other folders or containers.
        if (string.IsNullOrWhiteSpace(iconType) || !AllowedIconTypes.Contains(iconType))
        {
            returnResult.Message = "Invalid upload type.";
            return returnResult;
        }

        string extension = isCropped ? ".webp" : Path.GetExtension(file.FileName ?? file.Name);

        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            returnResult.Message = "Only image files (png, jpg, gif, webp, ico, bmp) are allowed.";
            return returnResult;
        }

        if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedContentTypes.Contains(file.ContentType))
        {
            returnResult.Message = "Only image files (png, jpg, gif, webp, ico, bmp) are allowed.";
            return returnResult;
        }

        string guid = Guid.NewGuid().ToString();
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
        string? connectionString = configuration["AzureBlobStorage:ConnectionString"];

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentNullException(path, "Azure storage connection string must be configured in AzureBlobStorage:ConnectionString");
        }

        BlobServiceClient blobServiceClient = new(connectionString);

        string? containerName = configuration["AzureBlobStorage:ContainerName"];

        if (string.IsNullOrEmpty(containerName))
        {
            throw new ArgumentNullException(path, "Azure storage container name must be configured in AzureBlobStorage:ContainerName");
        }

        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        BlobClient blobClient = containerClient.GetBlobClient(path);

        stream.Position = 0;

        // Blob names are server-generated GUIDs, so collisions indicate a bug or tampering:
        // never silently overwrite an existing blob.
        await blobClient.UploadAsync(stream, overwrite: false);

        return PathToUrl(blobClient.Uri);
    }

    private string PathToUrl(Uri uri)
    {
        return Uri.UnescapeDataString(uri.ToString()).Replace(_separator, "/");
    }

    private static string GetDbPath(string iconType, string guid, string extension)
    {
        string fileName = guid + extension;

        return Path.Combine(iconType.ToLowerInvariant(), fileName);
    }
}
