
namespace AIChatAssistant.Application.ServiceInterfaces;

public interface IFileParsingService
{
    public Task<string> ExtractTextFromFileAsync(Stream fileStream, string fileType);
}
