using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tokenizers.HuggingFace.Tokenizer;
using AIChatAssistant.Application.ServiceInterfaces;
using AIChatAssistant.Shared.Configuration;

namespace AIChatAssistant.Infrastructure.Services;

public class TokenizerService : ITokenizerService, IDisposable
{
    private readonly Tokenizer _tokenizer;
    private readonly ILogger<TokenizerService>? _logger;

    private const int MaxSequenceLength = 512;

    public TokenizerService(IOptions<AiModelSettings> tokenizerJsonPath, ILogger<TokenizerService>? logger = null)
    {
        var path = tokenizerJsonPath.Value.TokenizerJsonPath;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Tokenizer path cannot be null or empty", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Tokenizer file not found at: {path}");
        }

        try
        {
            // tokenizer.json from HuggingFace
            _tokenizer = Tokenizer.FromFile(path);

            _logger?.LogInformation("Tokenizer loaded successfully from {Path}", path);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load tokenizer from {Path}", path);
            throw new InvalidOperationException($"Cannot initialize tokenizer from {path}", ex);
        }
    }

    public int CountTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        try
        {
            var encoding = _tokenizer.Encode(text, true);
            return encoding.First().Ids.Count;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error counting tokens");
            return EstimateTokenCount(text);
        }
    }

    public bool IsWithinLimit(string text, int maxTokens = MaxSequenceLength)
    {
        return CountTokens(text) <= maxTokens;
    }

    public int[] Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<int>();
        }

        try
        {
            var encoding = _tokenizer.Encode(text, false);
            return encoding.First().Ids.Select(id => (int)id).ToArray();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error tokenizing text");
            throw;
        }
    }

    public string TruncateToTokenLimit(string text, int maxTokens = MaxSequenceLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        try
        {
            var encoding = _tokenizer.Encode(text, false);
            var ids = encoding.First().Ids;

            if (ids.Count <= maxTokens)
            {
                return text;
            }

            var truncatedIds = ids.Take(maxTokens).ToArray();
            return _tokenizer.Decode(truncatedIds, false) ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error truncating text to {MaxTokens} tokens", maxTokens);

            // Fallback
            int estimatedChars = maxTokens * 4;
            return text.Length <= estimatedChars
                ? text
                : text.Substring(0, Math.Min(estimatedChars, text.Length));
        }
    }

    private int EstimateTokenCount(string text)
    {
        int charCount = text.Length;
        int wordCount = text.Split([' ', '\n', '\t'],
            StringSplitOptions.RemoveEmptyEntries).Length;

        int charBasedEstimate = (int)Math.Ceiling(charCount / 4.0);
        int wordBasedEstimate = (int)(wordCount * 1.5);

        return Math.Max(charBasedEstimate, wordBasedEstimate);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}