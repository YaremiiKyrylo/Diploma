using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AIChatAssistant.Domain.Entities;

namespace AIChatAssistant.Domain.Entities.AiServiceDbEntities;

[Table("ChatSessions")]
public class ChatSession
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;

    [MaxLength(200)]
    public string Title { get; set; } = "New chat";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}