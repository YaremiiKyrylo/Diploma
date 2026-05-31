
namespace AIChatAssistant.Domain.DTO.AiModelItems;
public class ChatMessageResponse
{
    public Guid SessionId { get; set; }
    public string Reply { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<RetrievedChunkDto> Sources { get; set; } = new();
}