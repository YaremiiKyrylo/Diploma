using AIChatAssistant.Application.ServiceInterfaces;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIChatAssistant.Infrastructure.Services;
public class BlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(IConfiguration configuration, ILogger<BlobStorageService> logger)
    {
        _logger = logger;

        var connectionString = configuration["AzureBlobStorage:ConnectionString"];
        var containerName = configuration["AzureBlobStorage:ContainerName"];

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("AzureBlobStorage:ConnectionString is missing or empty.");

        if (string.IsNullOrWhiteSpace(containerName))
            throw new InvalidOperationException("AzureBlobStorage:ContainerName is missing or empty.");

        var blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
    {
        try
        {
            // Private container; avoids failures when storage account disallows public blob access. 
            await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

            // Generate a unique file name to avoid conflicts
            var uniqueFileName = $"{Guid.NewGuid()}-{Path.GetFileName(fileName)}";
            var blobClient = _containerClient.GetBlobClient(uniqueFileName);

            // Set HTTP headers (important for correct browser display)
            var blobHttpHeaders = new BlobHttpHeaders { ContentType = contentType };

            fileStream.Position = 0; // Reset the stream position just in case
            await blobClient.UploadAsync(fileStream, new BlobUploadOptions { HttpHeaders = blobHttpHeaders });

            _logger.LogInformation("Successfully uploaded file {FileName} to Azure Blob", uniqueFileName);

            return blobClient.Uri.AbsoluteUri; // Return a direct link to the CDN 
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName} to Azure", fileName);
            throw;
        }
    }

    public async Task<byte[]> DownloadFileAsync(string blobUrlOrName, CancellationToken cancellationToken = default)
    {
        try
        {
            var blobName = ResolveBlobName(blobUrlOrName);
            var blobClient = _containerClient.GetBlobClient(blobName);

            await using var ms = new MemoryStream();
            await blobClient.DownloadToAsync(ms, cancellationToken);
            ms.Position = 0;

            if (ms.Length == 0)
                throw new InvalidOperationException($"Blob '{blobName}' is empty.");

            _logger.LogInformation("Downloaded blob {BlobName} from Azure", blobName);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading blob {BlobUrlOrName}", blobUrlOrName);
            throw;
        }
    }

    public async Task DeleteFileAsync(string fileUrl)
    {
        try
        {
            var blobName = ResolveBlobName(fileUrl);
            var blobClient = _containerClient.GetBlobClient(blobName);

            await blobClient.DeleteIfExistsAsync();
            _logger.LogInformation("Deleted file {BlobName} from Azure", blobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file by url {Url}", fileUrl);
        }
    }

    private static string ResolveBlobName(string blobUrlOrName)
    {
        if (string.IsNullOrWhiteSpace(blobUrlOrName))
            throw new ArgumentException("Blob URL or name is required.", nameof(blobUrlOrName));

        if (!Uri.TryCreate(blobUrlOrName, UriKind.Absolute, out var uri))
            return blobUrlOrName.Trim();

        return Path.GetFileName(Uri.UnescapeDataString(uri.LocalPath));
    }
}