using AIChatAssistant.Domain.Entities.AiServiceDbEntities;
using AIChatAssistant.Domain.RepositoryInterfaces;
using AIChatAssistant.Infrastructure.Persistence.AiServiceDbContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AIChatAssistant.Infrastructure.Repositories;

public class ChatRepository : IChatRepository
{
    private readonly AiServiceDbContext _dbContext;
    private readonly ILogger<ChatRepository> _logger;
    public ChatRepository(AiServiceDbContext dbContext, ILogger<ChatRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }
    public async Task<int?> GetUserIdBySessionAsync(Guid sessionId, CancellationToken ct)
    {
        var chatSession = await _dbContext.ChatSessions

            .AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => s.UserId)
            .FirstOrDefaultAsync(ct);

        return chatSession;
    }

    public async Task AddMessageAsync(Guid sessionId, string text, string role, CancellationToken ct)
    {
        var message = new ChatMessage
        {
            SessionId = sessionId,
            Text = text,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.ChatMessages.AddAsync(message, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<List<ChatMessage>> GetSessionHistoryAsync(Guid sessionId, int limit, CancellationToken ct)
    {
        var messages = await _dbContext.ChatMessages

            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        messages.Reverse();

        return messages;
    }

    public async Task<Guid> CreateSessionAsync(int userId, string title, CancellationToken ct = default)
    {
        var session = new ChatSession

        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = string.IsNullOrWhiteSpace(title) ? "New chat" : title,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.ChatSessions.AddAsync(session, ct);
        await _dbContext.SaveChangesAsync(ct);

        return session.Id;
    }

    public async Task<string?> GetCustomSystemPromptAsync(CancellationToken ct = default)
    {
        try
        {
            return await _dbContext.UserChatSettings

                .AsNoTracking()
                .Where(s => !string.IsNullOrWhiteSpace(s.CustomSystemPrompt))
                .OrderByDescending(s => s.UpdatedAt)
                .Select(s => s.CustomSystemPrompt)
                .FirstOrDefaultAsync(ct);
        }
        catch (Exception ex) when (IsMissingTableOrUnavailable(ex))
        {
            _logger.LogWarning(ex, "UserChatSettings unavailable; using default system prompt.");
            return null;
        }
    }

    public async Task SaveCustomSystemPromptAsync(int userId, string prompt, CancellationToken ct = default)
    {
        try
        {
            var existing = await _dbContext.UserChatSettings

                .FirstOrDefaultAsync(s => s.UserId == userId, ct);

            if (existing == null)
            {
                await _dbContext.UserChatSettings.AddAsync(new UserChatSettings

                {
                    UserId = userId,
                    CustomSystemPrompt = prompt,
                    UpdatedAt = DateTime.UtcNow

                }, ct);
            }
            else
            {
                existing.CustomSystemPrompt = prompt;
                existing.UpdatedAt = DateTime.UtcNow;

            }
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (IsMissingTableOrUnavailable(ex))
        {
            _logger.LogError(ex, "Cannot save custom system prompt: UserChatSettings table missing or unavailable.");
            throw;
        }
    }

    private static bool IsMissingTableOrUnavailable(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is SqlException sql &&
                (sql.Number == 208 || sql.Number == 4060 || sql.Number == 18456))          
            {
                return true;
            }
        }
        return false;
    }
}
