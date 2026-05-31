using AIChatAssistant.Application.DTOs;
using AIChatAssistant.Domain.DTO.AiCallModels;
using AIChatAssistant.Domain.DTO.AiModelItems;
using AIChatAssistant.Domain.DTO.AiModelItems.Gemini;
using AIChatAssistant.Domain.Entities.AiServiceDbEntities;
using AIChatAssistant.Domain.Service_Interfaces;
using AIChatAssistant.Shared.Configuration;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIChatAssistant.Application.Services.LLM;
public class GeminiCallService : ILlmCallService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiSettings _config;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    public GeminiCallService(HttpClient httpClient, IOptions<LLMConfiguration> options)
    {
        _httpClient = httpClient;
        _config = options.Value.Gemini;
    }
    public async Task<string> GetResponse(BaseLlmRequest request)
    {
        var geminiRequest = new GeminiRequest
        {
            Contents = request.Messages
        .Where(m => m.Role != ChatRole.System)
        .Select(m => new Content
        {
            Role = m.Role == ChatRole.User ? "user" : "model",
            Parts = new List<Part> { new Part { Text = m.Content } }
        }).ToList(),
            GenerationConfig = new GenerationConfig
            {
                Temperature = request.Temperature,
                MaxOutputTokens = request.MaxTokens ?? 1024
            }
        };

        var systemMessage = request.Messages.FirstOrDefault(m => m.Role == ChatRole.System);
        if (systemMessage != null)
        {
            geminiRequest.SystemInstruction = new SystemInstruction
            {
                Parts = new Part { Text = systemMessage.Content }
            };
        }
        var url = $"{_config.ApiUrl}/v1beta/models/{_config.ModelId}:generateContent?key={_config.ApiKey}";
        var json = JsonSerializer.Serialize(geminiRequest, _jsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();
        var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseContent, _jsonOptions);
        return geminiResponse?.Candidates?[0]?.Content?.Parts?[0]?.Text ?? string.Empty;
    }
}