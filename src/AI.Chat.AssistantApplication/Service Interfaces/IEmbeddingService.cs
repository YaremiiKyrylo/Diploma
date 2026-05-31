
namespace AIChatAssistant.Application.ServiceInterfaces;

public interface IEmbeddingService
{
    public Task<float[]> GetEmbeddingAsync(string text, bool isQuery = false);
}
