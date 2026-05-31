using Microsoft.Extensions.DependencyInjection;
using AIChatAssistant.Application.ServiceInterfaces;
using AIChatAssistant.Application.Services;
using AIChatAssistant.Infrastructure.Services;

namespace AIChatAssistant.Application.DI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
        services.AddSingleton<IFileParsingService, FileParsingService>();
        services.AddSingleton<ITextChunkingService, TextChunkingService>();
        services.AddSingleton<IEmbeddingService, EmbeddingService>();
        services.AddSingleton<ITokenizerService, TokenizerService>();
        return services;
    }
}
