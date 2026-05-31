using AIChatAssistant.Domain.Entities.AiServiceDbEntities;

namespace AIChatAssistant.Domain.RepositoryInterfaces;

public interface IChatRepository
{
    Task<int?> GetUserIdBySessionAsync(Guid sessionId, CancellationToken ct);

    Task AddMessageAsync(Guid sessionId, string text, string role, CancellationToken ct);

    Task<List<ChatMessage>> GetSessionHistoryAsync(Guid sessionId, int limit, CancellationToken ct);

    Task<Guid> CreateSessionAsync(int userId, string title, CancellationToken ct = default);

    /// <summary>
    /// Returns the most recently updated admin system prompt, or null if none / table unavailable.
    /// </summary>
    Task<string?> GetCustomSystemPromptAsync(CancellationToken ct = default);

    Task SaveCustomSystemPromptAsync(int userId, string prompt, CancellationToken ct = default);
}