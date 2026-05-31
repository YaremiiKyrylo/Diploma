namespace AIChatAssistant.Domain.DTO.AiModelItems;
public class ChatMessageRequest
{
    public Guid SessionId { get; set; }

    public string Message { get; set; } = string.Empty;

    public int TopK { get; set; } = 5;

    public List<int>? SourceIds { get; set; }

}
