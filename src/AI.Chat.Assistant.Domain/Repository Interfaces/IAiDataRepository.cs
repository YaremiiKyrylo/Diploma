using AIChatAssistant.Domain.Entities;

namespace AIChatAssistant.Domain.RepositoryInterfaces;

public interface IAiDataRepository
{
    Task<KnowledgeSource?> GetByIdAsync(int id);
    Task<List<KnowledgeSource>> GetByProfileIdAsync(int profileId);
    Task<List<KnowledgeSource>> GetPendingDocumentsAsync();
    Task AddAsync(KnowledgeSource document);
    void Remove(KnowledgeSource document);
    Task<bool> ExistsAsync(int id);
}