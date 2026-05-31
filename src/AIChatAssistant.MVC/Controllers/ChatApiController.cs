using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIChatAssistant.Application.ServiceInterfaces;
using AIChatAssistant.Domain.RepositoryInterfaces;
using AIChatAssistant.Domain.DTO.AiModelItems;
using AIChatAssistant.MVC.Models.DTO;

namespace AIChatAssistant.MVC.Controllers;

[Authorize]
[Route("api/chat")]
[ApiController]
public class ChatApiController : ControllerBase
{
    private readonly IAiChatService _chatService;
    private readonly IChatRepository _chatRepository;
    private readonly ILogger<ChatApiController> _logger;

    public ChatApiController(
        IAiChatService chatService,
        IChatRepository chatRepository,
        ILogger<ChatApiController> logger)
    {
        _chatService = chatService;
        _chatRepository = chatRepository;
        _logger = logger;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] JsChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required." });

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out var userId))
            return StatusCode(500, new { error = "Invalid user ID." });

        Guid currentSessionId;
        if (request.SessionId == null || request.SessionId == Guid.Empty)
        {
            currentSessionId = await _chatRepository.CreateSessionAsync(userId, "Chat " + DateTime.Now.ToString("g"));
        }
        else
        {
            var sessionOwnerId = await _chatRepository.GetUserIdBySessionAsync(request.SessionId.Value, ct);
            if (!sessionOwnerId.HasValue || sessionOwnerId.Value != userId)
                return Unauthorized(new { error = "Chat session not found or access denied." });

            currentSessionId = request.SessionId.Value;
        }

        var serviceRequest = new ChatMessageRequest
        {
            SessionId = currentSessionId,
            Message = request.Message
        };

        try
        {
            var response = await _chatService.HandleChatMessageAsync(serviceRequest);
            return Ok(new
            {
                sessionId = response.SessionId,
                reply = response.Reply
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat send failed");
            return StatusCode(500, new { error = "An error occurred while processing the message." });
        }
    }
}
