using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AIChatAssistant.Domain.Entities;

namespace AIChatAssistant.Domain.Entities;

[Table("FileKnowledgeSources")]
public class FileKnowledgeSource : KnowledgeSource
{
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    public long FileSize { get; set; }

    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string? ParsedText { get; set; }

    /// <summary>Azure blob URI after admin/API upload.</summary>
    [MaxLength(2000)]
    public string? BlobUrl { get; set; }
}