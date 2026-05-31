using System.Security.Claims;
using AIChatAssistant.Application.ServiceInterfaces;
using AIChatAssistant.Domain.Entities;
using AIChatAssistant.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace AIChatAssistant.MVC.Controllers;

[Authorize(Roles = "Admin")]
[Route("KnowledgeBase")]
public class KnowledgeBaseController : Controller
{
    private readonly IDocumentProcessingService _documentProcessingService;
    private readonly ILogger<KnowledgeBaseController> _logger;
    private readonly string? _blobContainerName;

    public KnowledgeBaseController(
        IDocumentProcessingService documentProcessingService,
        ILogger<KnowledgeBaseController> logger,
        IConfiguration configuration)
    {
        _documentProcessingService = documentProcessingService;
        _logger = logger;
        _blobContainerName = configuration["AzureBlobStorage:ContainerName"];
    }

    [HttpGet("Sources")]
    public async Task<IActionResult> Sources([FromQuery] int? userId, CancellationToken ct)
    {
        var sources = await _documentProcessingService.GetAdminFileSourcesAsync(userId, ct);
        return Ok(sources.Select(MapSourceDto));
    }

    [HttpPost("Upload")]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "File is required" });

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        try
        {
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            var source = await _documentProcessingService.UploadFileToBlobAsync(
                createdByUserId: userId,
                fileName: file.FileName,
                contentType: file.ContentType ?? "application/octet-stream",
                fileBytes: bytes,
                ct: ct);

            return Ok(new
            {
                documentId = source.Id,
                status = source.Status.ToString(),
                blobUrl = source.BlobUrl,
                message = "File uploaded to storage. Use Process for RAG when ready."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin upload failed for UserId={UserId}", userId);
            var message = BlobStorageErrorHelper.GetUserMessage(ex, _blobContainerName);
            var status = ex is InvalidOperationException ? 400 : 502;
            return StatusCode(status, new { error = message });
        }
    }

    [HttpPost("process/{sourceId:int}")]
    public async Task<IActionResult> Process(int sourceId, CancellationToken ct)
    {
        try
        {
            var processed = await _documentProcessingService.ProcessExistingFileAsync(sourceId, ct);
            return Ok(new
            {
                documentId = processed.Id,
                status = processed.Status.ToString(),
                message = "File processed for RAG."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin process failed for SourceId={SourceId}", sourceId);
            return StatusCode(500, new { error = "Processing failed. Check logs." });
        }
    }

    /// <summary>Full delete: Pinecone vectors, DB source/chunks, and Azure blob.</summary>
    [HttpDelete("source/{sourceId:int}")]
    public async Task<IActionResult> DeleteSource(int sourceId, [FromQuery] bool deleteBlob = true, CancellationToken ct = default)
    {
        try
        {
            await _documentProcessingService.DeleteSourceAsync(sourceId, deleteBlob, ct);
            var message = deleteBlob
                ? "Knowledge source and blob deleted."
                : "RAG data cleared; blob retained. Use Process for RAG to re-index.";
            return Ok(new { message, sourceId, deleteBlob });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin delete failed for SourceId={SourceId}", sourceId);
            return StatusCode(500, new { error = "Delete failed. Check logs." });
        }
    }

    /// <summary>Deletes Pinecone vectors and DB chunks; keeps blob and source row (Pending).</summary>
    [HttpDelete("DeleteRelatedData/{sourceId:int}")]
    public Task<IActionResult> DeleteRelatedData(int sourceId, CancellationToken ct = default)
        => DeleteSource(sourceId, deleteBlob: false, ct);

    private static object MapSourceDto(FileKnowledgeSource s) => new
    {
        id = s.Id,
        fileName = s.FileName,
        status = s.Status.ToString(),
        ragStatus = s.Status.ToString(),
        blobUrl = s.BlobUrl,
        fileSize = s.FileSize,
        contentType = s.ContentType,
        createdByUserId = s.CreatedByUserId,
        createdAt = s.CreatedAt,
        processedAt = s.ProcessedAt
    };
}
