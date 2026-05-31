using AIChatAssistant.Application.DTOs;
using AIChatAssistant.Application.ServiceInterfaces;
using AIChatAssistant.Domain.DTO.AiModelItems;
using AIChatAssistant.Domain.DTO.Vectors;
using AIChatAssistant.Domain.Entities.AiServiceDbEntities;
using AIChatAssistant.Domain.RepositoryInterfaces;
using AIChatAssistant.Domain.Service_Interfaces;
using AIChatAssistant.Shared;
using Microsoft.Extensions.Logging;

namespace AIChatAssistant.Application.Services;

public class AiChatService : IAiChatService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IPineconeVectorRepository _vectorRepository;
    private readonly ILlmCallService _llmCallService;
    private readonly IChatRepository _chatRepository;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(
        IEmbeddingService embeddingService,
        IPineconeVectorRepository vectorRepository,
        ILlmCallService llmCallService,
        IChatRepository chatRepository,
        ILogger<AiChatService> logger)
    {
        _embeddingService = embeddingService;
        _vectorRepository = vectorRepository;
        _llmCallService = llmCallService;
        _chatRepository = chatRepository;
        _logger = logger;
    }

    public async Task<ChatMessageResponse> HandleChatMessageAsync(
        ChatMessageRequest request,
        CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("Сообщение не может быть пустым.", nameof(request));

        int topK = request.TopK is > 0 and <= 50 ? request.TopK : 5;

        _logger.LogInformation("Обработка сообщения для SessionId={SessionId}", request.SessionId);

        // 1. Валидация сессии и получение UserId из SQL базы
        var userId = await _chatRepository.GetUserIdBySessionAsync(request.SessionId, ct);
        if (!userId.HasValue)
        {
            throw new UnauthorizedAccessException($"Сессия {request.SessionId} не найдена или недоступна.");
        }

        // 2. Сохраняем сообщение пользователя в SQL
        await _chatRepository.AddMessageAsync(request.SessionId, request.Message, "user", ct);

        // 3. Получаем историю чата из SQL (последние 10 сообщений)
        var history = await _chatRepository.GetSessionHistoryAsync(request.SessionId, 10, ct);

        // 3b. Админский системный промпт из SQL (не из Pinecone / RAG)
        var customPrompt = await _chatRepository.GetCustomSystemPromptAsync(ct);

        // 4. Векторизация запроса пользователя
        var queryEmbedding = await _embeddingService.GetEmbeddingAsync(request.Message, isQuery: true);

        // 5. Векторный поиск в Pinecone (RAG)
        List<RetrievedChunkDto> searchResults;
        try
        {
            // Ищем контекст. Если нужно, фильтруем по конкретным документам (request.SourceIds)
            var sourceFilter = request.SourceIds?.FirstOrDefault();
            var vectorResults = await _vectorRepository.SearchAsync(
                queryVector: queryEmbedding,
                profileId: userId.Value,
                topK: topK,
                fileId: sourceFilter,
                cancellationToken: ct);

            searchResults = vectorResults.Select(v => new RetrievedChunkDto
            {
                SourceId = v.FileId,
                FileName = v.FileName ?? string.Empty,
                SimilarityScore = v.Score
            }).ToList();

            var contextText = BuildContextFromResults(vectorResults);

            // 7. Собираем финальный запрос для LLM
            var llmRequest = BuildLlmRequest(contextText, request.Message, history, customPrompt);

            // 8. Вызов LLM
            string reply;
            try
            {
                reply = await _llmCallService.GetResponse(llmRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка вызова LLM для SessionId={SessionId}", request.SessionId);
                throw;
            }

            // 9. Сохраняем ответ AI в SQL базу
            await _chatRepository.AddMessageAsync(request.SessionId, reply, "assistant", ct);

            _logger.LogInformation("Успешный ответ сгенерирован для SessionId={SessionId}", request.SessionId);

            // 10. Возвращаем ответ клиенту (вместе с источниками)
            return new ChatMessageResponse
            {
                SessionId = request.SessionId,
                Reply = reply,
                CreatedAt = DateTime.UtcNow,
                Sources = searchResults
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка поиска в Pinecone для SessionId={SessionId}", request.SessionId);
            throw;
        }
    }

    // Вспомогательный метод (остался почти без изменений, только типы поправили)
    private static string BuildContextFromResults(IEnumerable<VectorSearchResult> results)
    {
        var topResults = results.OrderByDescending(r => r.Score).ToList();
        if (!topResults.Any()) return string.Empty;

        var lines = new List<string>();
        foreach (var r in topResults)
        {
            if (!string.IsNullOrWhiteSpace(r.Text))
            {
                lines.Add($"[Источник: {r.FileName}, Точность: {r.Score:F2}]\n{r.Text}");
            }
        }
        return string.Join("\n\n", lines);
    }

    // Сборка массива сообщений для LLM
    private static BaseLlmRequest BuildLlmRequest(
        string contextText,
        string userMessage,
        List<ChatMessage> history, 
        string? customSystemPrompt)
    {
        var messages = new List<ChatMessageDto>(); 

        // 1. Системный промпт
        var systemPrompt = !string.IsNullOrWhiteSpace(customSystemPrompt)
          ? customSystemPrompt
          : "Don't indicate in your answer that you've read or just learned this information. Answer like a regular expert, as if you've always known the answer to this question.";

        messages.Add(new ChatMessageDto { Role = ChatRole.System, Content = systemPrompt });

        // 2. Инъекция RAG-контекста (притворяемся, что это системная инструкция или контекст)
        if (!string.IsNullOrWhiteSpace(contextText))
        {
            messages.Add(new ChatMessageDto
            {
                Role = ChatRole.System,
                Content = "Provided context:\n" + contextText
            });
        }

        // 3. Добавляем историю из БД
        if (history != null && history.Any())
        {
            foreach (var msg in history.OrderBy(m => m.CreatedAt))
            {
                var role = msg.Role.ToLower() == "assistant" ? ChatRole.Assistant : ChatRole.User;
                messages.Add(new ChatMessageDto { Role = role, Content = msg.Text });
            }
        }

        // 4. Добавляем текущее сообщение
        messages.Add(new ChatMessageDto { Role = ChatRole.User, Content = userMessage });

        return new BaseLlmRequest
        {
            Temperature = 0.3f,
            MaxTokens = 1024,
            Messages = messages
        };
    }
}