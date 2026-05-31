using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using AIChatAssistant.Shared.Exceptions;

namespace AIChatAssistant.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var (statusCode, title, detail, type, logLevel) = exception switch
        {
            NotFoundException ex => (
                StatusCodes.Status404NotFound,
                "Document was not found.",
                ex.Message,
                "https://httpstatuses.io/404",
                LogLevel.Warning 
            ),

            UnsupportedFileTypeException ex => (
                StatusCodes.Status415UnsupportedMediaType,
                "Unsupported file type. FileHandler can't process it.",
                ex.Message,
                "https://httpstatuses.io/415",
                LogLevel.Warning 
            ),

            InvalidFileFormatException ex => (
                StatusCodes.Status422UnprocessableEntity,
                "File parsing error. Corrupted file.",
                ex.Message,
                "https://httpstatuses.io/422",
                LogLevel.Warning
            ),

            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                "Internal Server Error. Please try again later.",
                "https://httpstatuses.io/500",
                LogLevel.Error 
            )
        };

        var traceId = context.TraceIdentifier;

        _logger.Log(
            logLevel,
            exception,
            "Exception occurred during request {Path}. Status: {StatusCode}, TraceId {TraceId}",
            context.Request.Path,
            statusCode,
            traceId);

        // ProblemDetails
        var problemDetails = new ProblemDetails
        {
            Type = type,
            Title = title,
            Detail = detail,
            Status = statusCode,
            Instance = context.Request.Path
        };
        problemDetails.Extensions["traceId"] = traceId;

        context.Response.StatusCode = statusCode;

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(problemDetails, options);

        await context.Response.WriteAsync(json);
    }
}