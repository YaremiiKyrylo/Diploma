using Microsoft.AspNetCore.Identity;
using AIChatAssistant.Domain.Entities.AiServiceDbEntities;

namespace AIChatAssistant.Domain.Entities;

public class ApplicationUser : IdentityUser<int>
{

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<KnowledgeSource> UploadedSources { get; set; } = new List<KnowledgeSource>();
    public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
    public UserChatSettings? ChatSettings { get; set; }
}