using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AIChatAssistant.Domain.Entities;

namespace AIChatAssistant.Domain.Entities;
public enum ChunkStatus { Pending, Indexed, Failed }

[Table("ContentChunks")]
public class ContentChunk
{
    [Key]
    public int Id { get; set; }
    [Required]
    public int SourceId { get; set; }

    [ForeignKey(nameof(SourceId))]
    public KnowledgeSource Source { get; set; } = null!;

    // Embeddings
    [Required]
    [MaxLength(200)]
    public string PineconeVectorId { get; set; } = string.Empty;

    public int ChunkIndex { get; set; }  // 0, 1, 2, 3...

    // Content
    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string Content { get; set; } = string.Empty;

    public int ContentLength { get; set; }
    public int? TokenCount { get; set; }

    // Status
    public ChunkStatus Status { get; set; } = ChunkStatus.Pending;
    public string? ErrorMessage { get; set; }

    // Embedding information
    public DateTime IndexedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}