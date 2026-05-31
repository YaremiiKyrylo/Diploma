namespace AIChatAssistant.Shared.Configuration;
public class GroqSettings
{
    public string ModelId { get; set; } = "llama-3.1-8b-instant";
    public string ApiUrl { get; set; } = "https://api.groq.com/openai/v1/chat/completions";
    public string ApiKey { get; set; } = string.Empty;
}