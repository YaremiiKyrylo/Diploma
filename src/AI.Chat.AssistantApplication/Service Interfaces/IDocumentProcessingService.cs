using AIChatAssistant.Domain.Entities;



namespace AIChatAssistant.Application.ServiceInterfaces;



public interface IDocumentProcessingService

{

    /// <summary>Upload to blob and create a <see cref="ProcessingStatus.Pending"/> file source (no RAG).</summary>

    Task<FileKnowledgeSource> UploadFileToBlobAsync(

        int createdByUserId,

        string fileName,

        string contentType,

        byte[] fileBytes,

        CancellationToken ct = default);



    /// <summary>Run RAG pipeline on an existing pending/failed file source (downloads from <see cref="FileKnowledgeSource.BlobUrl"/>).</summary>

    Task<KnowledgeSource> ProcessExistingFileAsync(int sourceId, CancellationToken ct = default);



    /// <summary>
    /// <paramref name="deleteBlob"/> = true: Pinecone vectors, DB source + chunks, and Azure blob.
    /// <paramref name="deleteBlob"/> = false: vectors + chunks only; keeps source row and blob (Pending).
    /// </summary>
    Task DeleteSourceAsync(int sourceId, bool deleteBlob = true, CancellationToken ct = default);



    Task<List<FileKnowledgeSource>> GetAdminFileSourcesAsync(int? userId = null, CancellationToken ct = default);



    Task<KnowledgeSource> ProcessFileAsync(

        int createdByUserId,

        string fileName,

        string contentType,

        byte[] fileBytes,

        string logicalType = "file",

        CancellationToken ct = default);



    Task<KnowledgeSource> ProcessTextAsync(

        int createdByUserId,

        string text,

        string logicalType = "text",

        CancellationToken ct = default);

}

