using AIChatAssistant.Application.DTOs;
using AIChatAssistant.Domain.DTO.AiCallModels;
using AIChatAssistant.Domain.Service_Interfaces;
using AIChatAssistant.Shared.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIChatAssistant.Application.Services.LLM;
public class GroqCallService : ILlmCallService
{
    private readonly HttpClient _httpClient;
    private readonly GroqSettings _config;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    public GroqCallService(HttpClient httpClient, IOptions<LLMConfiguration> options)
    {
        _httpClient = httpClient;
        _config = options.Value.Groq;
        _httpClient.BaseAddress = new Uri(_config.ApiUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", _config.ApiKey);
    }
    public async Task<string> GetResponse(BaseLlmRequest request)
    {
        var payload = new
        {
            model = _config.ModelId,
            messages = request.Messages.Select(m => new
            {
                role = m.Role.ToString().ToLower(),
                content = m.Content
            }),
            temperature = request.Temperature,
            max_tokens = request.MaxTokens
        };
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("", content);
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseString);
        return doc.RootElement

        .GetProperty("choices")[0]
        .GetProperty("message")
        .GetProperty("content")
        .GetString() ?? string.Empty;
    }
}