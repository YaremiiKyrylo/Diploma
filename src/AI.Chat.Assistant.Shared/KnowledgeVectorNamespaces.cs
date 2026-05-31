namespace AIChatAssistant.Shared;

/// <summary>
/// Pinecone namespace conventions for per-user and org-wide (admin) knowledge.
/// </summary>
public static class KnowledgeVectorNamespaces
{
    /// <summary>Profile id for admin-uploaded blob knowledge visible to all chat users.</summary>
    public const int SharedProfileId = 0;

    public static string GetNamespace(int profileId)
        => profileId == SharedProfileId ? "shared" : $"user_{profileId}";
}
