using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AIChatAssistant.Domain.Entities;

namespace AIChatAssistant.Domain.Entities;

[Table("TextKnowledgeSources")]
public class TextKnowledgeSource : KnowledgeSource
{
    [Required]
    [Column(TypeName = "nvarchar(max)")] 
    public string Content { get; set; } = string.Empty;
}