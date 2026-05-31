using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using AIChatAssistant.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using AIChatAssistant.Application.ServiceInterfaces;
using DocumentFormat.OpenXml;

namespace AIChatAssistant.Application.Services;

public class FileParsingService : IFileParsingService
{
    private readonly ILogger<FileParsingService> _logger;

    private const int InitialStringBuilderCapacity = 8192;
    private const int MaxEmptyLinesInRow = 2;

    public FileParsingService(ILogger<FileParsingService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExtractTextFromFileAsync(Stream fileStream, string fileType)
    {
        ArgumentNullException.ThrowIfNull(fileStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileType);

        if (!fileStream.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(fileStream));
        }

        try
        {
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }

            var normalizedType = NormalizeContentType(fileType);
            var rawText = normalizedType switch
            {
                "application/pdf" => ParsePdf(fileStream),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ParseDocx(fileStream),
                "text/plain" => await ParseTxtAsync(fileStream),
                _ => throw new UnsupportedFileTypeException($"Files with type '{fileType}' are not supported.")
            };

            return NormalizeText(rawText);
        }
        catch (UnsupportedFileTypeException)
        {
            throw;
        }
        catch (InvalidFileFormatException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during file parsing. Type: {ContentType}", fileType);
            throw new InvalidFileFormatException("The file is corrupted or has an invalid format.");
        }
    }

    private string ParsePdf(Stream stream)
    {
        var sb = new StringBuilder(InitialStringBuilderCapacity);

        try
        {
            using var pdf = PdfDocument.Open(stream);

            if (pdf.NumberOfPages == 0)
            {
                _logger.LogWarning("PDF document contains no pages");
                return string.Empty;
            }

            foreach (var page in pdf.GetPages())
            {
                var pageText = page.Text?.Trim();

                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    sb.AppendLine(pageText);
                    sb.AppendLine();
                }
            }

            _logger.LogDebug("Successfully parsed PDF with {PageCount} pages", pdf.NumberOfPages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing PDF document");
            throw new InvalidFileFormatException("Failed to parse PDF file. The file may be corrupted or password-protected.");
        }

        return sb.ToString();
    }

    private string ParseDocx(Stream stream)
    {
        var sb = new StringBuilder(InitialStringBuilderCapacity);

        try
        {
            using var wordDoc = WordprocessingDocument.Open(stream, false);

            if (wordDoc.MainDocumentPart?.Document?.Body == null)
            {
                _logger.LogWarning("DOCX document has no body content");
                return string.Empty;
            }

            var body = wordDoc.MainDocumentPart.Document.Body;

            ExtractHeadersAndFooters(wordDoc, sb);

            foreach (var element in body.Elements())
            {
                ProcessDocxElement(element, sb);
            }

            _logger.LogDebug("Successfully parsed DOCX document");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing DOCX document");
            throw new InvalidFileFormatException("Failed to parse DOCX file. The file may be corrupted.");
        }

        return sb.ToString();
    }

    private void ProcessDocxElement(DocumentFormat.OpenXml.OpenXmlElement element, StringBuilder sb)
    {
        switch (element)
        {
            case Paragraph paragraph:
                var paragraphText = paragraph.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(paragraphText))
                {
                    sb.AppendLine(paragraphText);
                }
                break;

            case Table table:
                ProcessTable(table, sb);
                break;

            default:
                var innerText = element.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(innerText))
                {
                    sb.AppendLine(innerText);
                }
                break;
        }
    }

    private void ProcessTable(Table table, StringBuilder sb)
    {
        foreach (var row in table.Elements<TableRow>())
        {
            var rowTexts = new List<string>();

            foreach (var cell in row.Elements<TableCell>())
            {
                var cellText = cell.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(cellText))
                {
                    rowTexts.Add(cellText);
                }
            }

            if (rowTexts.Any())
            {
                sb.AppendLine("| " + string.Join(" | ", rowTexts) + " |");
            }
        }
        sb.AppendLine();
    }

    private void ExtractHeadersAndFooters(WordprocessingDocument wordDoc, StringBuilder sb)
    {
        try
        {
            var headerParts = wordDoc.MainDocumentPart?.HeaderParts;
            if (headerParts != null)
            {
                foreach (var headerPart in headerParts)
                {
                    var headerText = headerPart.Header?.InnerText?.Trim();
                    if (!string.IsNullOrWhiteSpace(headerText))
                    {
                        sb.AppendLine(headerText);
                    }
                }
            }

            var footerParts = wordDoc.MainDocumentPart?.FooterParts;
            if (footerParts != null)
            {
                foreach (var footerPart in footerParts)
                {
                    var footerText = footerPart.Footer?.InnerText?.Trim();
                    if (!string.IsNullOrWhiteSpace(footerText))
                    {
                        sb.AppendLine(footerText);
                    }
                }
            }

            if (sb.Length > 0)
            {
                sb.AppendLine();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract headers/footers from DOCX");
        }
    }

    private async Task<string> ParseTxtAsync(Stream stream)
    {
        try
        {
            using var reader = new StreamReader(
                stream,
                encoding: Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                leaveOpen: true
            );

            var content = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("TXT file is empty");
                return string.Empty;
            }

            _logger.LogDebug("Successfully parsed TXT file");
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing TXT file");
            throw new InvalidFileFormatException("Failed to parse TXT file. The file may have an unsupported encoding.");
        }
    }

    private static string NormalizeContentType(string fileType)
    {
        if (string.IsNullOrWhiteSpace(fileType))
            return fileType;

        var t = fileType.Trim().ToLowerInvariant();
        if (t.Contains("pdf", StringComparison.Ordinal))
            return "application/pdf";
        if (t.Contains("wordprocessingml") || t.Contains("docx", StringComparison.Ordinal)
            || t == "application/msword")
            return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        if (t.StartsWith("text/", StringComparison.Ordinal) || t.Contains("plain", StringComparison.Ordinal))
            return "text/plain";
        return t;
    }

    private string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        text = Regex.Replace(text, @"[ \t]{2,}", " ");

        var lines = text.Split('\n')
            .Select(line => line.Trim())
            .ToList();

        var normalized = new List<string>();
        int emptyLineCount = 0;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                emptyLineCount++;
                if (emptyLineCount <= MaxEmptyLinesInRow)
                {
                    normalized.Add(string.Empty);
                }
            }
            else
            {
                emptyLineCount = 0;
                normalized.Add(line);
            }
        }

        while (normalized.Count > 0 && string.IsNullOrWhiteSpace(normalized[0]))
        {
            normalized.RemoveAt(0);
        }

        while (normalized.Count > 0 && string.IsNullOrWhiteSpace(normalized[^1]))
        {
            normalized.RemoveAt(normalized.Count - 1);
        }

        var result = string.Join("\n", normalized);

        _logger.LogDebug("Text normalized: original length {OriginalLength}, normalized length {NormalizedLength}",
            text.Length, result.Length);

        return result;
    }
}