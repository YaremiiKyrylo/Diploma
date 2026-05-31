namespace AIChatAssistant.Application.ServiceInterfaces;

public interface IBlobStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);
    Task<byte[]> DownloadFileAsync(string blobUrlOrName, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string fileUrl);
}
