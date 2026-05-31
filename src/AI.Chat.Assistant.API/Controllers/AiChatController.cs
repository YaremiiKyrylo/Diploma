using Microsoft.AspNetCore.Mvc;
using AIChatAssistant.Application.ServiceInterfaces;
using AIChatAssistant.Domain.DTO.AiModelItems;
using AIChatAssistant.Domain.RepositoryInterfaces;
using System.ComponentModel.DataAnnotations;

namespace AIChatAssistant.API.Controllers;

[ApiController]
[Route("api/ai-chat")]
public class AiChatController : ControllerBase
{
    private readonly ILogger<AiChatController> _logger;
    private readonly IAiChatService _aiChatService;
    private readonly IChatRepository _chatRepository;

    public AiChatController(
        ILogger<AiChatController> logger,
        IAiChatService aiChatService,
        IChatRepository chatRepository)
    {
        _logger = logger;
        _aiChatService = aiChatService;
        _chatRepository = chatRepository;
    }

    /// <summary>
    /// Creates a chat session for API testing (requires an existing AspNetUsers.Id).
    /// </summary>
    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSessionAsync(
        [FromBody] CreateChatSessionRequest request,
        CancellationToken ct)
    {
        if (request == null || request.UserId <= 0)
            return BadRequest(new { error = "UserId must be a positive integer (existing AspNetUsers.Id)." });

        try
        {
            var title = string.IsNullOrWhiteSpace(request.Title)
                ? $"API chat {DateTime.UtcNow:yyyy-MM-dd HH:mm}"
                : request.Title.Trim();

            var sessionId = await _chatRepository.CreateSessionAsync(request.UserId, title, ct);
            return Created($"/api/ai-chat/sessions/{sessionId}", new { sessionId, userId = request.UserId, title });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating chat session for UserId={UserId}", request.UserId);
            return StatusCode(500, new { error = "Could not create chat session. Ensure UserId exists in AspNetUsers." });
        }
    }

    public record CreateChatSessionRequest(
        [Required] int UserId,
        string? Title = null);
    [HttpPost("messages")]
    public async Task<IActionResult> ChatSendMessageAsync([FromBody] ChatMessageRequest request, CancellationToken ct)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required" });

        try
        {
            var result = await _aiChatService.HandleChatMessageAsync(request, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, new { error = "An error occurred while processing the chat message." });
        }
    }
}