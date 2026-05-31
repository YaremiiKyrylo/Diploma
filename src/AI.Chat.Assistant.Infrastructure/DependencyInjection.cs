using AIChatAssistant.Application.ServiceInterfaces;
using AIChatAssistant.Application.Services;
using AIChatAssistant.Domain.RepositoryInterfaces;
using AIChatAssistant.Domain.Service_Interfaces;
using AIChatAssistant.Infrastructure.Persistence.AiServiceDbContext;
using AIChatAssistant.Infrastructure.Repositories;
using AIChatAssistant.Infrastructure.Services;
using AIChatAssistant.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIChatAssistant.Infrastructure.DI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var appSettings = configuration.GetSection(ApplicationSettings.SectionName).Get<ApplicationSettings>();

        if (appSettings == null || string.IsNullOrEmpty(appSettings.ConnectionString))
        {
            throw new InvalidOperationException("Connection string is not configured.");
        }

        services.AddDbContext<AiServiceDbContext>(options =>
        {
            options.UseSqlServer(appSettings.ConnectionString);
        });

        services.AddScoped<IAiChatService, AiChatService>();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IAiDataRepository, AiDataRepository>();
        services.AddScoped<IDocumentProcessingRepository, DocumentProcessingRepository>();
        services.AddScoped<IPineconeVectorRepository, PineconeVectorRepository>();

        services.AddSingleton<IBlobStorageService, BlobStorageService>();

        return services;
    }
}
