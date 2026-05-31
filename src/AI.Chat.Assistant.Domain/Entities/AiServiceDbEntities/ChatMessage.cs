using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIChatAssistant.Domain.Entities.AiServiceDbEntities;

[Table("ChatMessages")]
public class ChatMessage
{
    [Key]
    public int Id { get; set; }

    public Guid SessionId { get; set; }

    [ForeignKey(nameof(SessionId))]
    public ChatSession Session { get; set; } = null!;

    [Required]
    public string Text { get; set; }

    [MaxLength(50)]
    public string Role { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}