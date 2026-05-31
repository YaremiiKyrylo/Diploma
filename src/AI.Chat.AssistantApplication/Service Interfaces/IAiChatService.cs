using AIChatAssistant.Domain.DTO.AiModelItems;

namespace AIChatAssistant.Application.ServiceInterfaces;
public interface IAiChatService
{
    Task<ChatMessageResponse> HandleChatMessageAsync(ChatMessageRequest request, CancellationToken ct = default);
}
