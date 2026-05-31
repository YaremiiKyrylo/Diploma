using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AIChatAssistant.Domain.Entities;

namespace AIChatAssistant.Domain.Entities.AiServiceDbEntities;

/// <summary>
/// Per-user chat configuration. Admins store a custom system prompt here (not vectorized).
/// </summary>
[Table("UserChatSettings")]
public class UserChatSettings
{
    [Key]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;

    [Required]
    public string CustomSystemPrompt { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
