using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace AIChatAssistant.Domain.DTO.AiModelItems;

/// <summary>
/// Model for file upload with form data
/// </summary>
public class FileUploadRequest
{
    [Required]
    public int UserId { get; set; }

    [Required]
    public IFormFile File { get; set; } = null!;
}