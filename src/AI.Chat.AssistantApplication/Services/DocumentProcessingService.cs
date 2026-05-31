using Microsoft.Extensions.Logging;
using AIChatAssistant.Application.ServiceInterfaces;
using AIChatAssistant.Domain;
using AIChatAssistant.Domain.Entities;
using AIChatAssistant.Domain.RepositoryInterfaces;
using AIChatAssistant.Shared;

namespace AIChatAssistant.Infrastructure.Services;

public class DocumentProcessingService : IDocumentProcessingService
{
    private readonly IFileParsingService _parsingService;
    private readonly ITextChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IPineconeVectorRepository _vectorRepository;
    private readonly IDocumentProcessingRepository _repository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<DocumentProcessingService> _logger;

    public DocumentProcessingService(
        IFileParsingService parsingService,
        ITextChunkingService chunkingService,
        IEmbeddingService embeddingService,
        IPineconeVectorRepository vectorRepository,
        IDocumentProcessingRepository repository,
        IBlobStorageService blobStorageService,
        ILogger<DocumentProcessingService> logger)
    {
        _parsingService = parsingService;
        _chunkingService = chunkingService;
        _embeddingService = embeddingService;
        _vectorRepository = vectorRepository;
        _repository = repository;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    public async Task<FileKnowledgeSource> UploadFileToBlobAsync(
        int createdByUserId,
        string fileName,
        string contentType,
        byte[] fileBytes,
        CancellationToken ct = default)
    {
        await using var ms = new MemoryStream(fileBytes);
        var blobUrl = await _blobStorageService.UploadFileAsync(ms, fileName, contentType);

        var source = new FileKnowledgeSource
        {
            CreatedByUserId = createdByUserId,
            Type = SourceType.File,
            Status = ProcessingStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            FileName = fileName,
            FileSize = fileBytes.LongLength,
            ContentType = contentType,
            BlobUrl = blobUrl
        };

        await _repository.AddKnowledgeSourceAsync(source, ct);
        await _repository.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Uploaded file to blob. SourceId={SourceId}, BlobUrl={BlobUrl}",
            source.Id, blobUrl);

        return source;
    }

    public async Task<KnowledgeSource> ProcessExistingFileAsync(int sourceId, CancellationToken ct = default)
    {
        var source = await _repository.GetFileSourceByIdAsync(sourceId, ct)
            ?? throw new InvalidOperationException($"File knowledge source {sourceId} not found.");

        if (string.IsNullOrWhiteSpace(source.BlobUrl))
            throw new InvalidOperationException("Source has no BlobUrl; cannot process for RAG.");

        if (source.Status == ProcessingStatus.Processing)
            throw new InvalidOperationException("Source is already being processed.");

        if (source.Status == ProcessingStatus.Completed)
            throw new InvalidOperationException("Source is already vectorized. Delete and re-upload to replace.");

        var fileBytes = await _blobStorageService.DownloadFileAsync(source.BlobUrl, ct);
        if (fileBytes.Length == 0)
            throw new InvalidOperationException("Downloaded file from blob is empty.");

        return await RunFileRagPipelineAsync(source, fileBytes, ct);
    }

    public async Task DeleteSourceAsync(int sourceId, bool deleteBlob = true, CancellationToken ct = default)
    {
        var source = await _repository.GetFileSourceByIdAsync(sourceId, ct)
            ?? await _repository.GetSourceByIdAsync(sourceId, ct);

        if (source == null)
            throw new InvalidOperationException($"Knowledge source {sourceId} not found.");

        if (source.Status is ProcessingStatus.Completed or ProcessingStatus.Processing or ProcessingStatus.Failed)
            await DeleteVectorsForSourceAsync(source, ct);

        if (!deleteBlob)
        {
            await _repository.ClearChunksForSourceAsync(sourceId, ct);
            source.Status = ProcessingStatus.Pending;
            source.ProcessedAt = null;
            if (source is FileKnowledgeSource fileSource)
                fileSource.ParsedText = null;

            await _repository.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Cleared RAG data for SourceId={SourceId}; blob and source record retained.",
                sourceId);
            return;
        }

        var blobUrl = (source as FileKnowledgeSource)?.BlobUrl;

        await _repository.DeleteSourceByIdAsync(sourceId, ct);
        await _repository.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(blobUrl))
            await _blobStorageService.DeleteFileAsync(blobUrl);

        _logger.LogInformation(
            "Deleted knowledge source {SourceId} (vectors, DB, blob)",
            sourceId);
    }

    public Task<List<FileKnowledgeSource>> GetAdminFileSourcesAsync(int? userId = null, CancellationToken ct = default)
        => _repository.GetAllFileSourcesAsync(userId, ct);

    public async Task<KnowledgeSource> ProcessFileAsync(
        int createdByUserId,
        string fileName,
        string contentType,
        byte[] fileBytes,
        string logicalType = "file",
        CancellationToken ct = default)
    {
        var source = new FileKnowledgeSource
        {
            CreatedByUserId = createdByUserId,
            Type = SourceType.File,
            Status = ProcessingStatus.Processing,
            CreatedAt = DateTime.UtcNow,
            FileName = fileName,
            FileSize = fileBytes.LongLength,
            ContentType = contentType
        };

        await _repository.AddKnowledgeSourceAsync(source, ct);
        await _repository.SaveChangesAsync(ct);

        return await RunFileRagPipelineAsync(source, fileBytes, ct);
    }

    public async Task<KnowledgeSource> ProcessTextAsync(
        int createdByUserId,
        string text,
        string logicalType = "text",
        CancellationToken ct = default)
    {
        if (string.Equals(logicalType, "system-prompt", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "System prompts must be saved via UserChatSettings (Settings/UpdatePrompt), not ProcessTextAsync.");
        }

        var source = new TextKnowledgeSource
        {
            CreatedByUserId = createdByUserId,
            Type = SourceType.Text,
            Status = ProcessingStatus.Processing,
            CreatedAt = DateTime.UtcNow,
            Content = text,
        };

        await _repository.AddKnowledgeSourceAsync(source, ct);
        await _repository.SaveChangesAsync(ct);

        try
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("Input text is empty");

            var chunks = _chunkingService.SplitIntoChunks(
                text,
                sourceFileId: source.Id,
                maxTokensPerChunk: 300,
                overlapTokens: 50);

            if (chunks.Count == 0)
                throw new InvalidOperationException("No chunks were produced");

            var chunksWithEmbeddings = new List<(ContentChunk chunk, float[] embedding)>(chunks.Count);

            foreach (var chunk in chunks)
            {
                var embedding = await _embeddingService.GetEmbeddingAsync(chunk.Content, isQuery: false);

                chunk.Status = ChunkStatus.Indexed;
                chunk.CreatedAt = DateTime.UtcNow;
                chunk.IndexedAt = DateTime.UtcNow;

                chunksWithEmbeddings.Add((chunk, embedding));
            }

            await _repository.AddChunksAsync(chunksWithEmbeddings.Select(x => x.chunk), ct);
            await _repository.SaveChangesAsync(ct);

            await _vectorRepository.UpsertChunksBatchAsync(
                chunksWithEmbeddings,
                namespaceProfileId: createdByUserId,
                ownerUserId: createdByUserId,
                fileName: null,
                cancellationToken: ct);

            source.Status = ProcessingStatus.Completed;
            source.ProcessedAt = DateTime.UtcNow;
            await _repository.SaveChangesAsync(ct);

            return source;
        }
        catch (Exception ex)
        {
            source.Status = ProcessingStatus.Failed;
            await _repository.SaveChangesAsync(ct);

            _logger.LogError(ex, "Failed to process text document.");

            throw;
        }
    }

    private async Task<KnowledgeSource> RunFileRagPipelineAsync(
        FileKnowledgeSource source,
        byte[] fileBytes,
        CancellationToken ct)
    {
        if (source.Chunks.Count > 0)
        {
            await DeleteVectorsForSourceAsync(source, ct);
            await _repository.ClearChunksForSourceAsync(source.Id, ct);
            source.ParsedText = null;
            await _repository.SaveChangesAsync(ct);
        }

        source.Status = ProcessingStatus.Processing;
        await _repository.SaveChangesAsync(ct);

        try
        {
            await using var ms = new MemoryStream(fileBytes);
            var text = await _parsingService.ExtractTextFromFileAsync(ms, source.ContentType);

            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("Parsed text is empty");

            source.ParsedText = text;
            _logger.LogInformation(
                "Parsed file SourceId={SourceId}: {CharCount} chars, ContentType={ContentType}",
                source.Id, text.Length, source.ContentType);

            var chunks = _chunkingService.SplitIntoChunks(
                text,
                sourceFileId: source.Id,
                maxTokensPerChunk: 300,
                overlapTokens: 50);

            if (chunks.Count == 0)
                throw new InvalidOperationException("No chunks were produced");

            var chunksWithEmbeddings = new List<(ContentChunk chunk, float[] embedding)>(chunks.Count);

            foreach (var chunk in chunks)
            {
                var embedding = await _embeddingService.GetEmbeddingAsync(chunk.Content, isQuery: false);

                chunk.Status = ChunkStatus.Indexed;
                chunk.CreatedAt = DateTime.UtcNow;
                chunk.IndexedAt = DateTime.UtcNow;

                chunksWithEmbeddings.Add((chunk, embedding));
            }

            await _repository.AddChunksAsync(chunksWithEmbeddings.Select(x => x.chunk), ct);
            await _repository.SaveChangesAsync(ct);

            var vectorProfileId = KnowledgeVectorProfile.ResolveProfileId(source);
            await _vectorRepository.UpsertChunksBatchAsync(
                chunksWithEmbeddings,
                namespaceProfileId: vectorProfileId,
                ownerUserId: source.CreatedByUserId,
                fileName: source.FileName,
                cancellationToken: ct);

            source.Status = ProcessingStatus.Completed;
            source.ProcessedAt = DateTime.UtcNow;
            await _repository.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Successfully processed file. SourceId={SourceId}, Chunks={ChunkCount}, VectorNamespace={Namespace}, OwnerUserId={OwnerUserId}",
                source.Id, chunks.Count, KnowledgeVectorNamespaces.GetNamespace(vectorProfileId), source.CreatedByUserId);

            return source;
        }
        catch (Exception ex)
        {
            source.Status = ProcessingStatus.Failed;
            await _repository.SaveChangesAsync(ct);

            _logger.LogError(ex, "Failed to process file document.");

            throw;
        }
    }

    private async Task DeleteVectorsForSourceAsync(KnowledgeSource source, CancellationToken ct)
    {
        var targetProfileId = KnowledgeVectorProfile.ResolveProfileId(source);
        var primaryNamespace = KnowledgeVectorNamespaces.GetNamespace(targetProfileId);

        _logger.LogInformation(
            "Deleting Pinecone vectors for SourceId={SourceId} in namespace {Namespace} (profile {ProfileId})",
            source.Id, primaryNamespace, targetProfileId);

        await _vectorRepository.DeleteFileVectorsAsync(source.Id, targetProfileId, ct);

        // Legacy: admin blob files were once indexed under user_{ownerId} before shared namespace.
        if (targetProfileId == KnowledgeVectorNamespaces.SharedProfileId
            && source.CreatedByUserId != KnowledgeVectorNamespaces.SharedProfileId)
        {
            var legacyNamespace = KnowledgeVectorNamespaces.GetNamespace(source.CreatedByUserId);
            _logger.LogInformation(
                "Also deleting legacy vectors for SourceId={SourceId} in namespace {Namespace}",
                source.Id, legacyNamespace);
            await _vectorRepository.DeleteFileVectorsAsync(source.Id, source.CreatedByUserId, ct);
        }
    }
}
