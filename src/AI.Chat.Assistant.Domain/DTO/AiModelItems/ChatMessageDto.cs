namespace AIChatAssistant.Domain.DTO.AiModelItems;

public class ChatMessageDto
{
    public ChatRole Role { get; set; } = ChatRole.User;
    public string Content { get; set; } = string.Empty;
}

public enum ChatRole
{
    User,
    Assistant,
    System
}