namespace AIChatAssistant.Domain.DTO.Vectors;

public class VectorSearchResult
{
    public string? Id { get; set; }
    public float Score { get; set; }
    public string? Text { get; set; }
    public int FileId { get; set; }
    public int ChunkIndex { get; set; }
    public string? FileName { get; set; }
    public DateTime CreatedAt { get; set; }
}