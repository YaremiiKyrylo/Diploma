using AIChatAssistant.Domain.Entities;

namespace AIChatAssistant.Application.ServiceInterfaces;

public interface ITextChunkingService
{
    public List<ContentChunk> SplitIntoChunks(string text, int sourceFileId, int maxTokensPerChunk = 300, int overlapTokens = 50);
}
