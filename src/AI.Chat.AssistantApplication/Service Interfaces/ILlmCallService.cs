using AIChatAssistant.Application.DTOs;

namespace AIChatAssistant.Domain.Service_Interfaces;

public interface ILlmCallService
{
    public Task<string> GetResponse(BaseLlmRequest request);
}
