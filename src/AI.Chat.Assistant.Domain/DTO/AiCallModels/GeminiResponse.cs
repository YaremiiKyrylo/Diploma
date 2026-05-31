using System.Text.Json.Serialization;
using AIChatAssistant.Domain.DTO.AiModelItems.Gemini;

namespace AIChatAssistant.Domain.DTO.AiCallModels;

public class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<Candidate>? Candidates { get; set; }
}
