
namespace AIChatAssistant.Application.ServiceInterfaces;

public interface ITokenizerService
{
    public int CountTokens(string text);
    public int[] Tokenize(string text);
    public string TruncateToTokenLimit(string text, int maxTokens);
}
