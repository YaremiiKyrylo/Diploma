using AIChatAssistant.Domain.DTO.Vectors;
using AIChatAssistant.Domain.Entities;

namespace AIChatAssistant.Domain.RepositoryInterfaces;

public interface IPineconeVectorRepository
{
    Task UpsertChunksBatchAsync(
        IEnumerable<(ContentChunk chunk, float[] embedding)> chunksWithEmbeddings,
        int namespaceProfileId,
        int ownerUserId,
        string? fileName = null,
        CancellationToken cancellationToken = default);

    Task DeleteFileVectorsAsync(int fileId, int profileId, CancellationToken cancellationToken = default);

    Task<List<VectorSearchResult>> SearchAsync(
        float[] queryVector,
        int profileId,
        int topK = 5,
        int? fileId = null,
        CancellationToken cancellationToken = default);

    Task<List<VectorSearchResult>> SearchAcrossNamespacesAsync(
        float[] queryVector,
        IReadOnlyCollection<int> profileIds,
        int topK = 5,
        int? fileId = null,
        CancellationToken cancellationToken = default);
}
