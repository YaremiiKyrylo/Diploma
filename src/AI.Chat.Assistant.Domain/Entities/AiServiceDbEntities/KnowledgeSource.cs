using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIChatAssistant.Domain.Entities;

/// <summary>
/// RAG pipeline state. <see cref="Pending"/> = uploaded to blob, not yet vectorized (admin "Process for RAG").
/// </summary>
public enum ProcessingStatus { Pending, Processing, Completed, Failed }
public enum SourceType { File, Text }

[Table("KnowledgeSources")]
public class KnowledgeSource
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int CreatedByUserId { get; set; }

    [ForeignKey(nameof(CreatedByUserId))]
    public ApplicationUser Author { get; set; } = null!;

    public SourceType Type { get; set; }
    public ProcessingStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    public ICollection<ContentChunk> Chunks { get; set; } = new List<ContentChunk>();
}