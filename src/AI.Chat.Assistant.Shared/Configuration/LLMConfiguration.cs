namespace AIChatAssistant.Shared.Configuration;

public class LLMConfiguration
{
    public GroqSettings Groq { get; set; } = new();
    public GeminiSettings Gemini { get; set; } = new();
}