using AIChatAssistant.Domain.Entities;

namespace AIChatAssistant.Domain.RepositoryInterfaces;
public interface IDocumentProcessingRepository
{
    Task<KnowledgeSource?> GetSourceByIdAsync(int id, CancellationToken ct = default);
    Task AddKnowledgeSourceAsync(KnowledgeSource source, CancellationToken ct = default);
    Task AddChunksAsync(IEnumerable<ContentChunk> chunks, CancellationToken ct = default);
    Task<List<KnowledgeSource>> GetSourcesByUserIdAsync(int userId, CancellationToken ct = default);
    Task DeleteSourcesByUserIdAsync(int userId, CancellationToken ct = default);
    Task<FileKnowledgeSource?> GetFileSourceByIdAsync(int id, CancellationToken ct = default);
    Task<List<FileKnowledgeSource>> GetAllFileSourcesAsync(int? userId = null, CancellationToken ct = default);
    Task<List<int>> GetCompletedKnowledgeOwnerIdsAsync(CancellationToken ct = default);

    /// <summary>Returns distinct Pinecone profile ids for all completed knowledge sources.</summary>
    Task<List<int>> GetCompletedVectorProfileIdsAsync(CancellationToken ct = default);
    Task<int> GetPendingFileSourceCountAsync(CancellationToken ct = default);
    Task DeleteSourceByIdAsync(int sourceId, CancellationToken ct = default);
    Task ClearChunksForSourceAsync(int sourceId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}