
namespace AIChatAssistant.Domain.DTO.AiModelItems.Gemini;

public class Content
{
    public string? Role { get; set; }
    public List<Part> Parts { get; set; } = [];
}
