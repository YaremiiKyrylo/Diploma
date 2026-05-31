namespace AIChatAssistant.Domain.DTO.AiModelItems;

public class RetrievedChunkDto
{
    public int SourceId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
}