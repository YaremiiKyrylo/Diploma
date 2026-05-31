using AIChatAssistant.Application.ServiceInterfaces;
using AIChatAssistant.Domain.DTO.AiModelItems;
using AIChatAssistant.Domain.RepositoryInterfaces;
using AIChatAssistant.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace AIChatAssistant.Controllers;

/// <summary>
/// Get data from Blob Storage and process it into knowledge sources and vectors.
/// </summary>
[ApiController]
[Route("api/knowledge")]
public class KnowledgeController : ControllerBase
{
    private readonly IDocumentProcessingService _documentProcessingService;
    private readonly IDocumentProcessingRepository _repository;
    private readonly IPineconeVectorRepository _vectorRepository;
    private readonly ILogger<KnowledgeController> _logger;
    private readonly HttpClient _httpClient;
    private readonly IBlobStorageService _blobStorageService;

    public KnowledgeController(
        IDocumentProcessingService documentProcessingService,
        IDocumentProcessingRepository repository,
        IPineconeVectorRepository vectorRepository,
        ILogger<KnowledgeController> logger,
        IBlobStorageService blobStorageService,
        IHttpClientFactory httpClientFactory)
    {
        _documentProcessingService = documentProcessingService;
        _repository = repository;
        _vectorRepository = vectorRepository;
        _logger = logger;
        _blobStorageService = blobStorageService;
        _httpClient = httpClientFactory.CreateClient();
    }

    [HttpPost("upload-file")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> UploadAndProcessFile(
        [FromForm] FileUploadRequest request,
        CancellationToken ct)
    {
        if (request?.File == null || request.File.Length <= 0)
            return BadRequest(new { error = "File is required and cannot be empty" });

        try
        {
            await using var sourceStream = request.File.OpenReadStream();
            await using var copyStream = new MemoryStream();
            await sourceStream.CopyToAsync(copyStream, ct);
            copyStream.Position = 0;

            var fileBytes = copyStream.ToArray();

            // Upload to blob storage
            var blobUrl = await _blobStorageService.UploadFileAsync(
                copyStream,
                request.File.FileName,
                request.File.ContentType ?? "application/octet-stream");

            // Process the file
            var processed = await _documentProcessingService.ProcessFileAsync(
                createdByUserId: request.UserId,
                fileName: request.File.FileName,
                contentType: request.File.ContentType ?? "application/octet-stream",
                fileBytes: fileBytes,
                ct: ct);

            return Ok(new
            {
                userId = request.UserId,
                blobUrl,
                documentId = processed.Id,
                status = processed.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading and processing file for UserId={UserId}", request.UserId);
            throw;
        }
    }

    [HttpPost("upload-text")]
    public async Task<IActionResult> UploadAndProcessText(
        [FromBody, Required] UploadTextRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "Text is required" });

        await using var textStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(request.Text));
        var blobUrl = await _blobStorageService.UploadFileAsync(textStream, $"{Guid.NewGuid()}.txt", "text/plain");

        var processed = await _documentProcessingService.ProcessTextAsync(
            createdByUserId: request.UserId,
            text: request.Text,
            ct: ct);

        return Ok(new
        {
            request.UserId,
            blobUrl,
            documentId = processed.Id,
            status = processed.Status
        });
    }

    [HttpPost("file")]
    public async Task<IActionResult> ProcessFileFromBlob(
        [FromBody, Required] KnowledgeSourceRequest request,
        CancellationToken ct)
    {
        if (request.SourceType != "file")
            return BadRequest(new { error = "Invalid source type" });

        if (string.IsNullOrWhiteSpace(request.BlobUrl))
            return BadRequest(new { error = "BlobUrl is required" });

        if (string.IsNullOrWhiteSpace(request.FileName))
            return BadRequest(new { error = "FileName is required" });

        try
        {
            var fileBytes = await _httpClient.GetByteArrayAsync(request.BlobUrl, ct);
            if (fileBytes == null || fileBytes.Length == 0)
                return BadRequest(new { error = "Downloaded file is empty" });

            var contentType = request.ContentType ?? "application/octet-stream";

            var source = await _documentProcessingService.ProcessFileAsync(
                createdByUserId: request.UserId,
                fileName: request.FileName,
                contentType: contentType,
                fileBytes: fileBytes,
                logicalType: "file",
                ct: ct);

            return Ok(new
            {
                userId = request.UserId,
                documentId = source.Id,
                status = source.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing file from Blob. UserId={UserId}, BlobUrl={BlobUrl}, FileName={FileName}",
                request.UserId, request.BlobUrl, request.FileName);
            // Optionally map to ProblemDetails
            throw;
        }
    }

    [HttpPost("text")]
    public async Task<IActionResult> ProcessTextFromBlob(
        [FromBody, Required] KnowledgeSourceRequest request,
        CancellationToken ct)
    {
        if (request.SourceType != "text")
            return BadRequest(new { error = "Invalid source type" });

        if (string.IsNullOrWhiteSpace(request.BlobUrl))
            return BadRequest(new { error = "BlobUrl is required" });

        var textContent = await _httpClient.GetStringAsync(request.BlobUrl, ct);
        if (string.IsNullOrWhiteSpace(textContent))
            return BadRequest(new { error = "Downloaded text is empty" });

        var source = await _documentProcessingService.ProcessTextAsync(
            createdByUserId: request.UserId,
            text: textContent,
            logicalType: "text",
            ct: ct);

        return Ok(new
        {
            userId = request.UserId,
            documentId = source.Id,
            status = source.Status
        });
    }

    /// <summary>
    /// Deletes a single knowledge source. Default removes vectors, DB, and blob.
    /// Pass <paramref name="deleteBlob"/> = false to keep the blob for re-processing.
    /// </summary>
    [HttpDelete("source/{sourceId:int}")]
    public async Task<IActionResult> DeleteSource(
        int sourceId,
        [FromQuery] bool deleteBlob = true,
        CancellationToken ct = default)
    {
        try
        {
            await _documentProcessingService.DeleteSourceAsync(sourceId, deleteBlob, ct);
            var message = deleteBlob
                ? "Knowledge source and blob deleted."
                : "RAG data cleared; blob retained.";
            return Ok(new { message, sourceId, deleteBlob });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting source SourceId={SourceId}", sourceId);
            throw;
        }
    }

    /// <summary>
    /// Deletes all knowledge sources and vectors for a specific item.
    /// Called by the monolith when a ChatAiItem is deleted or re-processed.
    /// </summary>
    [HttpDelete("user/{userId}")]
    public async Task<IActionResult> DeleteUserKnowledge(
        int userId,
        CancellationToken ct)
    {
        try
        {
            var sources = await _repository.GetSourcesByUserIdAsync(userId, ct);

            if (sources.Count == 0)
                return Ok(new { message = "No sources found for user", userId });

            // Delete vectors from Pinecone for each source
            foreach (var source in sources)
            {
                await _vectorRepository.DeleteFileVectorsAsync(source.Id, userId, ct);
            }

            // Delete sources and chunks from DB
            await _repository.DeleteSourcesByUserIdAsync(userId, ct);
            await _repository.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Deleted {Count} knowledge sources for UserId={UserId}",
                sources.Count, userId);

            return Ok(new { message = "Knowledge deleted", userId, deletedSources = sources.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting knowledge for UserId={UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Model for requesting Blob Storage
    /// </summary>
    public record KnowledgeSourceRequest(
        [Required] int UserId,
        [Required] string SourceType,  // "text", "file",
        [Required] string BlobUrl,
        string? FileName = null,       // for file
        string? ContentType = null,    // for file
        DateTime? CreatedAt = null
    );

    public record UploadTextRequest(
        [Required] int UserId,
        [Required] string Text
    );
}
        