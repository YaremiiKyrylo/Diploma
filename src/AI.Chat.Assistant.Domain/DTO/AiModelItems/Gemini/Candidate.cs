using System.Text.Json.Serialization;

namespace AIChatAssistant.Domain.DTO.AiModelItems.Gemini;

public class Candidate
{
    [JsonPropertyName("content")]
    public Content? Content { get; set; }
}
