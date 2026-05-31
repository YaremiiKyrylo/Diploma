using Microsoft.Extensions.Logging;
using AIChatAssistant.Application.ServiceInterfaces;
using AIChatAssistant.Domain.Entities;

namespace AIChatAssistant.Infrastructure.Services;
public class TextChunkingService : ITextChunkingService
{
    private readonly ILogger<TextChunkingService> _logger;
    private readonly ITokenizerService _tokenizerService;

    private const int MaxTokensPerChunk = 512;

    private readonly string[] _separators = ["\n\n", "\n", ". ", "! ", "? ", "; ", ", ", " "];

    public TextChunkingService(
        ILogger<TextChunkingService> logger,
        ITokenizerService tokenizerService)
    {
        _logger = logger;
        _tokenizerService = tokenizerService;
    }

    public List<ContentChunk> SplitIntoChunks(
        string text,
        int sourceFileId,
        int maxTokensPerChunk = 300,
        int overlapTokens = 50)
    {
        var chunks = new List<ContentChunk>();

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Attempted to chunk empty text for file {SourceFileId}", sourceFileId);
            return chunks;
        }

        // Validation
        if (maxTokensPerChunk > MaxTokensPerChunk)
        {
            _logger.LogWarning(
                "Requested chunk size {RequestedSize} exceeds model limit {MaxLimit}. Using {MaxLimit} instead.",
                maxTokensPerChunk, MaxTokensPerChunk, MaxTokensPerChunk);
            maxTokensPerChunk = MaxTokensPerChunk;
        }

        if (overlapTokens >= maxTokensPerChunk)
        {
            throw new ArgumentException(
                $"Overlap ({overlapTokens}) must be less than chunk size ({maxTokensPerChunk})",
                nameof(overlapTokens));
        }

        try
        {
            // Split text with tokens
            var textChunks = SplitByTokens(text, maxTokensPerChunk, overlapTokens);

            _logger.LogInformation(
                "Split file {SourceFileId} into {ChunkCount} chunks (max tokens: {MaxTokens}, overlap: {Overlap})",
                sourceFileId, textChunks.Count, maxTokensPerChunk, overlapTokens);

            for (int index = 0; index < textChunks.Count; index++)
            {
                var content = textChunks[index];
                var tokenCount = _tokenizerService.CountTokens(content);

                chunks.Add(new ContentChunk
                {
                    SourceId = sourceFileId,
                    ChunkIndex = index,
                    PineconeVectorId = $"doc_{sourceFileId}_chunk_{index}",
                    Content = content,
                    ContentLength = content.Length,
                    TokenCount = tokenCount,
                    Status = ChunkStatus.Pending
                });

                // Warning if chunk is too big
                if (tokenCount > MaxTokensPerChunk)
                {
                    _logger.LogWarning(
                        "Chunk {Index} for file {FileId} exceeds model limit: {TokenCount} > {MaxLimit}",
                        index, sourceFileId, tokenCount, MaxTokensPerChunk);
                }
            }

            return chunks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while chunking text for file {SourceFileId}", sourceFileId);
            throw;
        }
    }

    /// <summary>
    /// Splits text into chunks based on the number of tokens
    /// </summary>
    private List<string> SplitByTokens(string text, int maxTokens, int overlapTokens)
    {
        var chunks = new List<string>();

        // Base case: all text fits
        if (_tokenizerService.CountTokens(text) <= maxTokens)
        {
            return new List<string> { text.Trim() };
        }

        // Find the separator
        string? separator = FindBestSeparator(text);

        if (separator == null)
        {
            // Hard slicing by tokens
            return HardSplitByTokens(text, maxTokens, overlapTokens);
        }

        // Split by separator
        var parts = text.Split(new[] { separator }, StringSplitOptions.None)
        .Where(p => !string.IsNullOrWhiteSpace(p))
        .Select(p => p.Trim())
        .ToList();

        var currentChunk = new List<string>();
        int currentTokenCount = 0;

        foreach (var part in parts)
        {
            int partTokens = _tokenizerService.CountTokens(part);

            // If the part itself is greater than the limit, split recursively
            if (partTokens > maxTokens)
            {
                // Save the accumulated
                if (currentChunk.Any())
                {
                    chunks.Add(string.Join(separator, currentChunk).Trim());
                    currentChunk.Clear();
                    currentTokenCount = 0;
                }

                // Recursively process the larger part
                var subChunks = SplitByTokens(part, maxTokens, overlapTokens);
                chunks.AddRange(subChunks);
                continue;
            }

            // Check if the part fits in the current chunk
            int separatorTokens = currentChunk.Any()
            ? _tokenizerService.CountTokens(separator)
            : 0;

            if (currentTokenCount + separatorTokens + partTokens > maxTokens && currentChunk.Any())
            {
                // Save the current chunk
                var chunkText = string.Join(separator, currentChunk).Trim();
                chunks.Add(chunkText);

                // Apply overlap: retain the last parts
                ApplyTokenOverlap(currentChunk, ref currentTokenCount, separator, overlapTokens);
            }

            // Add a part
            currentChunk.Add(part);
            currentTokenCount += partTokens + separatorTokens;
        }

        // Add the remainder
        if (currentChunk.Any())
        {
            chunks.Add(string.Join(separator, currentChunk).Trim());
        }

        return chunks;
    }

    private void ApplyTokenOverlap(
        List<string> buffer,
        ref int currentTokenCount,
        string separator,
        int targetOverlapTokens)
    {
        // Remove elements from the beginning until we reach the desired overlap
        while (currentTokenCount > targetOverlapTokens && buffer.Count > 0)
        {
            var removedPart = buffer[0];
            int removedTokens = _tokenizerService.CountTokens(removedPart);

            if (buffer.Count > 1)
            {
                removedTokens += _tokenizerService.CountTokens(separator);
            }

            currentTokenCount -= removedTokens;
            buffer.RemoveAt(0);
        }
    }

    private List<string> HardSplitByTokens(string text, int maxTokens, int overlapTokens)
    {
        var result = new List<string>();
        var allTokenIds = _tokenizerService.Tokenize(text);

        int position = 0;
        int step = maxTokens - overlapTokens;

        while (position < allTokenIds.Length)
        {
            int chunkSize = Math.Min(maxTokens, allTokenIds.Length - position);
            var chunkTokenIds = allTokenIds.Skip(position).Take(chunkSize).ToArray();

            var chunkText = _tokenizerService.TruncateToTokenLimit(text, chunkSize);
            result.Add(chunkText);

            position += step;

            if (position >= allTokenIds.Length)
                break;
        }

        return result;
    }

    private string? FindBestSeparator(string text)
    {
        foreach (var separator in _separators)
        {
            if (text.Contains(separator))
            {
                return separator;
            }
        }
        return null;
    }
}