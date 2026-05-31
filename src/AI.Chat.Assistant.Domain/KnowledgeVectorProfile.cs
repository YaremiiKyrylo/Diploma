using AIChatAssistant.Domain.Entities;

namespace AIChatAssistant.Domain;

/// <summary>
/// Maps knowledge sources to Pinecone namespace profile ids.
/// </summary>
public static class KnowledgeVectorProfile
{
    public const int SharedProfileId = 0;

    /// <summary>
    /// Admin blob uploads are indexed in the shared namespace; API/direct uploads use the owner namespace.
    /// </summary>
    public static int ResolveProfileId(KnowledgeSource source)
    {
        if (source is FileKnowledgeSource { BlobUrl: { } blob } && !string.IsNullOrWhiteSpace(blob))
            return SharedProfileId;
        return source.CreatedByUserId;
    }
}
