
namespace AIChatAssistant.Domain.DTO.Vectors;
public class VectorIndexStats
{
    public int TotalVectors { get; set; }
    public string Namespace { get; set; } = string.Empty;
    public Dictionary<string, int>? Dimensions { get; set; }
}