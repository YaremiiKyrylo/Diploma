namespace AIChatAssistant.Shared.Configuration;

public class ApplicationSettings
{
    public const string SectionName = "ApplicationSettings";
    public string ConnectionString { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}