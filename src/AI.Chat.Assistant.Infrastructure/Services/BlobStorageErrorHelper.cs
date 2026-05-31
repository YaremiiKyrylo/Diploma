using Azure;

namespace AIChatAssistant.Infrastructure.Services;

public static class BlobStorageErrorHelper
{
    public static string GetUserMessage(Exception ex, string? configuredContainer = null)
    {
        var inner = ex.InnerException ?? ex;
        if (inner is RequestFailedException azure)
            return MapRequestFailed(azure, configuredContainer);

        if (ex is RequestFailedException direct)
            return MapRequestFailed(direct, configuredContainer);

        if (IsMissingConfiguration(ex))
            return "Azure Blob Storage is not configured. Set AzureBlobStorage:ConnectionString and AzureBlobStorage:ContainerName in appsettings.Development.json.";

        return "Upload to Azure Blob Storage failed. Check application logs for details.";
    }

    private static string MapRequestFailed(RequestFailedException ex, string? configuredContainer)
    {
        var containerHint = string.IsNullOrWhiteSpace(configuredContainer)
            ? "AzureBlobStorage:ContainerName"
            : $"container '{configuredContainer}' (AzureBlobStorage:ContainerName)";

        return ex.Status switch
        {
            404 => $"Blob container not found. Verify {containerHint} matches an existing container in your storage account (names are case-sensitive).",
            403 => "Azure Blob access denied. Confirm the connection string uses a valid AccountKey from Access keys, and that the key has not been rotated.",
            401 => "Azure Blob authentication failed. Check AzureBlobStorage:ConnectionString in appsettings.Development.json (AccountName and AccountKey must match the storage account).",
            409 when ex.ErrorCode is "ContainerAlreadyExists" or "PublicAccessNotPermitted"
                => "Cannot create or configure the blob container (public access may be disabled on the storage account). Use an existing private container and set ContainerName to match exactly.",
            _ => $"Azure Blob error ({ex.Status}, {ex.ErrorCode}): {ex.Message}"
        };
    }

    private static bool IsMissingConfiguration(Exception ex)
    {
        var message = ex.Message;
        return message.Contains("connectionString", StringComparison.OrdinalIgnoreCase)
               || message.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase)
               || message.Contains("No valid combination", StringComparison.OrdinalIgnoreCase);
    }
}
