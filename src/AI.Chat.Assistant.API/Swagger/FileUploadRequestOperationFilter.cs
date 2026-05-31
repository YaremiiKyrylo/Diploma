using AIChatAssistant.Domain.DTO.AiModelItems;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AIChatAssistant.API.Swagger;

/// <summary>
/// Expands <see cref="FileUploadRequest"/> into multipart/form-data fields for Swagger.
/// </summary>
public class FileUploadRequestOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasFileUploadRequest = context.ApiDescription.ParameterDescriptions
            .Any(p => p.Type == typeof(FileUploadRequest));

        if (!hasFileUploadRequest)
            return;

        operation.RequestBody = new OpenApiRequestBody
        {
            Content =
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Required = new HashSet<string> { "UserId", "File" },
                        Properties =
                        {
                            ["UserId"] = new OpenApiSchema { Type = "integer", Format = "int32" },
                            ["File"] = new OpenApiSchema { Type = "string", Format = "binary" }
                        }
                    }
                }
            }
        };

        operation.Parameters.Clear();
    }
}
