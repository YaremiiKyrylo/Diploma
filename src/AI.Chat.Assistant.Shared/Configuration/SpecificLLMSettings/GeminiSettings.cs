namespace AIChatAssistant.Shared.Configuration;
public class GeminiSettings
{
    public string ModelId { get; set; } = "llama-3.1-8b-instant";
    public string ApiUrl { get; set; } = "https://generativelanguage.googleapis.com";
    public string ApiKey { get; set; } = string.Empty;
}