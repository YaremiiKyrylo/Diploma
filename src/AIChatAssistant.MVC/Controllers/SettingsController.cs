using System.Security.Claims;
using AIChatAssistant.Domain.RepositoryInterfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIChatAssistant.MVC.Controllers;

[Authorize(Roles = "Admin")]
public class SettingsController : Controller
{
    private readonly IChatRepository _chatRepository;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        IChatRepository chatRepository,
        ILogger<SettingsController> logger)
    {
        _chatRepository = chatRepository;
        _logger = logger;
    }

    /// <summary>
    /// Persists the admin behavioral prompt in <c>UserChatSettings</c> only (no blob, no TextKnowledgeSource, no Pinecone).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> UpdatePrompt([FromForm] string systemPrompt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(systemPrompt))
            return BadRequest(new { error = "System prompt is required" });

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        try
        {
            await _chatRepository.SaveCustomSystemPromptAsync(userId, systemPrompt.Trim(), ct);

            return Ok(new
            {
                message = "System prompt saved (not indexed as RAG — document uploads remain the knowledge base)."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdatePrompt failed for UserId={UserId}", userId);
            return StatusCode(500, new { error = "Failed to save prompt." });
        }
    }
}
