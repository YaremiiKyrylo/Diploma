using AIChatAssistant.Domain.DTO.AiModelItems.Gemini;

namespace AIChatAssistant.Domain.DTO.AiCallModels;

public class GeminiRequest
{
    public SystemInstruction? SystemInstruction { get; set; }

    public required List<Content> Contents { get; set; } = [];

    public GenerationConfig? GenerationConfig { get; set; }
}