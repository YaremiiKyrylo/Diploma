using System.ComponentModel.DataAnnotations;

namespace AIChatAssistant.Shared.Configuration;
public class PineconeSettings
{
    public const string SectionName = "PineconeSettings";
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string IndexName { get; set; } = "chat-assistant-embeddings";

    public int Dimension { get; set; } = 384;

    public string Metric { get; set; } = "cosine";
}