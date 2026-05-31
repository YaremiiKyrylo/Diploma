namespace AIChatAssistant.Domain.DTO.AiModelItems.Gemini;

public class GenerationConfig
{
    public float Temperature { get; set; } = 0.7f;
    public int MaxOutputTokens { get; set; } = 500;
}
