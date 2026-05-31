using Microsoft.Extensions.Options;
using AIChatAssistant.Shared.Configuration;

namespace AIChatAssistant.Middleware;

public class ApiKeyAuthMiddleware
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private readonly RequestDelegate _next;
    private readonly string _apiKey;
    private readonly IHostEnvironment _environment;

    public ApiKeyAuthMiddleware(
        RequestDelegate next,
        IOptions<ApplicationSettings> settings,
        IHostEnvironment environment)
    {
        _next = next;
        _apiKey = settings.Value.ApiKey;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip auth for Swagger and dev diagnostics
        if (context.Request.Path.StartsWithSegments("/swagger")
            || (_environment.IsDevelopment() && context.Request.Path.StartsWithSegments("/api/diag")))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey)
            || string.IsNullOrWhiteSpace(_apiKey)
            || extractedApiKey != _apiKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or missing API key" });
            return;
        }

        await _next(context);
    }
}
