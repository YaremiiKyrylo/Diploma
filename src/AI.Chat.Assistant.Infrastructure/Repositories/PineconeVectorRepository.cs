using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pinecone;
using AIChatAssistant.Shared;
using AIChatAssistant.Shared.Configuration;
using AIChatAssistant.Domain.Entities;
using AIChatAssistant.Domain.DTO.Vectors;
using AIChatAssistant.Domain.RepositoryInterfaces;

namespace AIChatAssistant.Infrastructure.Repositories;

public class PineconeVectorRepository : IPineconeVectorRepository, IDisposable
{
    private readonly ILogger<PineconeVectorRepository> _logger;
    private readonly PineconeSettings _settings;
    private readonly PineconeClient _pineconeClient;
    private readonly Lazy<Task<IndexClient>> _indexClient;

    private const int MaxBatchSize = 100;

    public PineconeVectorRepository(
        ILogger<PineconeVectorRepository> logger,
        IOptions<PineconeSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;

        ValidateSettings();

        _pineconeClient = new PineconeClient(_settings.ApiKey);

        _indexClient = new Lazy<Task<IndexClient>>(async () =>
        {
            try
            {
                var index = _pineconeClient.Index(_settings.IndexName);
                _logger.LogInformation(
                    "Connected to Pinecone index: {IndexName}",
                    _settings.IndexName);
                return index;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to connect to Pinecone index: {IndexName}",
                    _settings.IndexName);
                throw;
            }
        });
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException("Pinecone API key is not configured");
        }

        if (string.IsNullOrWhiteSpace(_settings.IndexName))
        {
            throw new InvalidOperationException("Pinecone index name is not configured");
        }

        if (_settings.Dimension <= 0)
        {
            throw new InvalidOperationException("Invalid embedding dimension");
        }
    }

    private async Task<IndexClient> GetIndexClientAsync()
    {
        return await _indexClient.Value;
    }

    private static string GetNamespace(int profileId)
        => KnowledgeVectorNamespaces.GetNamespace(profileId);

    public async Task UpsertChunkAsync(
       ContentChunk chunk,
       float[] embedding,
       int profileId,
       CancellationToken cancellationToken = default)
    {
        try
        {
            // Validation
            ValidateEmbedding(embedding);

            var index = await GetIndexClientAsync();
            var @namespace = GetNamespace(profileId);

            var vector = CreateVector(chunk, embedding, profileId);

            await index.UpsertAsync(new UpsertRequest
            {
                Vectors = new[] { vector },
                Namespace = @namespace
            }, null, cancellationToken);

            _logger.LogDebug(
                "Upserted vector {VectorId} to namespace {Namespace}",
                vector.Id, @namespace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to upsert vector for chunk {ChunkId} of file {FileId}",
                chunk.ChunkIndex, chunk.SourceId);
            throw;
        }
    }

    public async Task UpsertChunksBatchAsync(
        IEnumerable<(ContentChunk chunk, float[] embedding)> chunksWithEmbeddings,
        int namespaceProfileId,
        int ownerUserId,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        var chunksList = chunksWithEmbeddings.ToList();

        if (!chunksList.Any())
        {
            _logger.LogWarning("Attempted to upsert empty batch");
            return;
        }

        try
        {
            var index = await GetIndexClientAsync();
            var @namespace = GetNamespace(namespaceProfileId);

            // Create batches (MaxBatchSize)
            var batches = chunksList
                .Select((item, index) => new { item, index })
                .GroupBy(x => x.index / MaxBatchSize)
                .Select(g => g.Select(x => x.item).ToList())
                .ToList();

            _logger.LogInformation(
                "Upserting {TotalVectors} vectors in {BatchCount} batches to namespace {Namespace} (ownerUserId={OwnerUserId})",
                chunksList.Count, batches.Count, @namespace, ownerUserId);

            // Send batches in parallel 
            var semaphore = new SemaphoreSlim(3); // Maximum 3 batches in parallel
            var tasks = batches.Select(async batch =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var vectors = batch
                        .Select(item => CreateVector(item.chunk, item.embedding, ownerUserId, fileName))
                        .ToList();

                    await index.UpsertAsync(new UpsertRequest
                    {
                        Vectors = vectors,
                        Namespace = @namespace
                    }, null, cancellationToken);

                    _logger.LogDebug("Upserted batch of {Count} vectors", vectors.Count);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation(
                "Successfully upserted {TotalVectors} vectors to namespace {Namespace}",
                chunksList.Count, @namespace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to upsert batch of {Count} vectors for namespace profile {ProfileId}",
                chunksList.Count, namespaceProfileId);
            throw;
        }
    }

    private Vector CreateVector(ContentChunk chunk, float[] embedding, int ownerUserId, string? fileName = null)
    {
        var vectorId = chunk.PineconeVectorId;

        var metadata = new Metadata
        {
            ["text"] = chunk.Content,
            ["file_id"] = chunk.SourceId,
            ["chunk_index"] = chunk.ChunkIndex,
            ["user_id"] = ownerUserId,
            ["token_count"] = chunk.TokenCount ?? 0,
            ["created_at"] = DateTime.UtcNow.ToString("o")
        };

        if (!string.IsNullOrWhiteSpace(fileName))
            metadata["file_name"] = fileName;

        return new Vector
        {
            Id = vectorId,
            Values = embedding,
            Metadata = metadata
        };
    }

    private void ValidateEmbedding(float[] embedding)
    {
        if (embedding == null || embedding.Length == 0)
        {
            throw new ArgumentException("Embedding cannot be null or empty");
        }

        if (embedding.Length != _settings.Dimension)
        {
            throw new ArgumentException(
                $"Embedding dimension mismatch. Expected {_settings.Dimension}, got {embedding.Length}");
        }

        if (embedding.Any(v => float.IsNaN(v) || float.IsInfinity(v)))
        {
            throw new ArgumentException("Embedding contains invalid values (NaN or Infinity)");
        }
    }

    public async Task<List<VectorSearchResult>> SearchAsync(
        float[] queryVector,
        int profileId,
        int topK = 5,
        int? fileId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateEmbedding(queryVector);

            if (topK <= 0 || topK > 100)
            {
                throw new ArgumentException("topK must be between 1 and 100");
            }

            var index = await GetIndexClientAsync();
            var @namespace = GetNamespace(profileId);

            // Make request
            var queryRequest = new QueryRequest
            {
                Vector = queryVector,
                TopK = (uint)topK,
                Namespace = @namespace,
                IncludeMetadata = true
            };

            // Add fileId filter
            if (fileId.HasValue)
            {
                queryRequest.Filter = new Metadata
                {
                    ["file_id"] = fileId.Value
                };
            }

            // Make search
            var response = await index.QueryAsync(queryRequest, null, cancellationToken);

            var results = (response.Matches ?? [])
                .Select(match => new VectorSearchResult
                {
                    Id = match.Id,
                    Score = match.Score ?? 0f,
                    Text = GetMetadataString(match.Metadata, "text"),
                    FileId = GetMetadataInt(match.Metadata, "file_id"),
                    ChunkIndex = GetMetadataInt(match.Metadata, "chunk_index"),
                    FileName = GetMetadataString(match.Metadata, "file_name"),
                    CreatedAt = GetMetadataDateTime(match.Metadata, "created_at")
                })
                .ToList();

            var withText = results.Count(r => !string.IsNullOrWhiteSpace(r.Text));
            if (results.Count > 0 && withText == 0)
            {
                _logger.LogWarning(
                    "Pinecone returned {MatchCount} matches in namespace {Namespace} but none had readable 'text' metadata",
                    results.Count, @namespace);
            }

            _logger.LogInformation(
                "Found {ResultCount} results ({WithText} with text) in namespace {Namespace}" +
                (fileId.HasValue ? " (filtered by file {FileId})" : ""),
                results.Count, withText, @namespace, fileId);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to search vectors for profile {ProfileId}",
                profileId);
            throw;
        }
    }

    public async Task<List<VectorSearchResult>> SearchAcrossNamespacesAsync(
        float[] queryVector,
        IReadOnlyCollection<int> profileIds,
        int topK = 5,
        int? fileId = null,
        CancellationToken cancellationToken = default)
    {
        var namespacesToQuery = (profileIds?.Count > 0
            ? profileIds.Distinct()
            : new[] { KnowledgeVectorNamespaces.SharedProfileId }).ToList();

        var merged = new List<VectorSearchResult>();
        foreach (var profileId in namespacesToQuery)
        {
            var batch = await SearchAsync(queryVector, profileId, topK, fileId, cancellationToken);
            merged.AddRange(batch);
        }

        var topResults = merged
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();

        _logger.LogInformation(
            "Merged {TotalMatches} matches from {NamespaceCount} namespaces into top {TopK} (fileFilter={FileId})",
            merged.Count, namespacesToQuery.Count, topK, fileId);

        return topResults;
    }

    public async Task DeleteFileVectorsAsync(
        int fileId,
        int profileId,
        CancellationToken cancellationToken = default)
    {
        var @namespace = GetNamespace(profileId);
        try
        {
            var index = await GetIndexClientAsync();

            await index.DeleteAsync(new DeleteRequest
            {
                Filter = new Metadata
                {
                    ["file_id"] = fileId
                },
                Namespace = @namespace
            }, null, cancellationToken);

            _logger.LogInformation(
                "Deleted all vectors for file {FileId} in namespace {Namespace}",
                fileId, @namespace);
        }
        catch (Exception ex) when (IsPineconeNotFound(ex))
        {
            _logger.LogWarning(
                "No vectors to delete for file {FileId} in namespace {Namespace} (profile {ProfileId}); treating as success",
                fileId, @namespace, profileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to delete vectors for file {FileId} of profile {ProfileId} (namespace {Namespace})",
                fileId, profileId, @namespace);
            throw;
        }
    }

    public async Task DeleteUserVectorsAsync(
        int profileId,
        CancellationToken cancellationToken = default)
    {
        var @namespace = GetNamespace(profileId);
        try
        {
            var index = await GetIndexClientAsync();

            await index.DeleteAsync(new DeleteRequest
            {
                DeleteAll = true,
                Namespace = @namespace
            }, null, cancellationToken);

            _logger.LogInformation(
                "Deleted all vectors for profile {ProfileId} (namespace {Namespace})",
                profileId, @namespace);
        }
        catch (Exception ex) when (IsPineconeNotFound(ex))
        {
            _logger.LogWarning(
                "No vectors to delete for profile {ProfileId} in namespace {Namespace}; treating as success",
                profileId, @namespace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to delete all vectors for profile {ProfileId} (namespace {Namespace})",
                profileId, @namespace);
            throw;
        }
    }

    /// <summary>
    /// Pinecone returns gRPC NOT_FOUND (status 5) when the namespace or filtered vectors do not exist.
    /// For deletes this is equivalent to already removed.
    /// </summary>
    private static bool IsPineconeNotFound(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase)
                || message.Contains("status code 5", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<VectorIndexStats> GetIndexStatsAsync(
        int profileId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var index = await GetIndexClientAsync();
            var @namespace = GetNamespace(profileId);

            // Get index stats
            var stats = await index.DescribeIndexStatsAsync(new DescribeIndexStatsRequest
            {
                Filter = new Metadata()  //Can add filters
            }, null, cancellationToken);

            var namespaceStats = stats.Namespaces?.TryGetValue(@namespace, out var ns) == true
                ? ns
                : null;

            return new VectorIndexStats
            {
                TotalVectors = (int)(namespaceStats?.VectorCount ?? 0),
                Namespace = @namespace
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to get index stats for profile {ProfileId}",
                profileId);
            throw;
        }
    }

    private static string GetMetadataString(Metadata? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value))
            return string.Empty;

        if (value.TryPickT0(out var text, out _))
            return text ?? string.Empty;

        return string.Empty;
    }

    private static int GetMetadataInt(Metadata? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value))
            return 0;

        if (value.TryPickT0(out var s, out _) && int.TryParse(s, out var fromString))
            return fromString;
        if (value.TryPickT1(out var d, out _) && d >= int.MinValue && d <= int.MaxValue)
            return (int)d;
        return 0;
    }

    private static DateTime GetMetadataDateTime(Metadata? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value))
            return DateTime.MinValue;

        if (value.TryPickT0(out var s, out _) && DateTime.TryParse(s, out var dt))
            return dt;
        return DateTime.MinValue;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

}
