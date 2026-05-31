using AIChatAssistant.Domain.DTO.AiModelItems;

namespace AIChatAssistant.Application.DTOs;

public class BaseLlmRequest
{
    public float Temperature { get; set; } = 0.7f;
    public int? MaxTokens { get; set; }
    public required List<ChatMessageDto> Messages { get; set; } = [];
}