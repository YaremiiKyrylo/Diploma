using Microsoft.EntityFrameworkCore;
using AIChatAssistant.Domain;
using AIChatAssistant.Domain.Entities;
using AIChatAssistant.Infrastructure.Persistence.AiServiceDbContext;
using AIChatAssistant.Domain.RepositoryInterfaces;

namespace AIChatAssistant.Infrastructure.Repositories;
public class DocumentProcessingRepository : IDocumentProcessingRepository
{
    private readonly AiServiceDbContext _dbContext;

    public DocumentProcessingRepository(AiServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<KnowledgeSource?> GetSourceByIdAsync(int id, CancellationToken ct = default)
    {
        // Tracking is useful here because we update Status, ProcessedAt, etc.
        return await _dbContext.KnowledgeSources
            .Include(s => s.Chunks)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task AddKnowledgeSourceAsync(KnowledgeSource source, CancellationToken ct = default)
    {
        await _dbContext.KnowledgeSources.AddAsync(source, ct);
    }

    public async Task AddChunksAsync(IEnumerable<ContentChunk> chunks, CancellationToken ct = default)
    {
        await _dbContext.ContentChunks.AddRangeAsync(chunks, ct);
    }

    public async Task<List<KnowledgeSource>> GetSourcesByUserIdAsync(int userId, CancellationToken ct = default)
    {
        return await _dbContext.KnowledgeSources
            .Include(s => s.Chunks)
            .Where(s => s.CreatedByUserId == userId)
            .ToListAsync(ct);
    }

    public async Task DeleteSourcesByUserIdAsync(int userId, CancellationToken ct = default)
    {
        var sources = await _dbContext.KnowledgeSources
            .Include(s => s.Chunks)
            .Where(s => s.CreatedByUserId == userId)
            .ToListAsync(ct);

        foreach (var source in sources)
        {
            _dbContext.ContentChunks.RemoveRange(source.Chunks);
        }
        _dbContext.KnowledgeSources.RemoveRange(sources);
    }

    public async Task<FileKnowledgeSource?> GetFileSourceByIdAsync(int id, CancellationToken ct = default)
    {
        return await _dbContext.Set<FileKnowledgeSource>()
            .Include(s => s.Chunks)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<List<FileKnowledgeSource>> GetAllFileSourcesAsync(int? userId = null, CancellationToken ct = default)
    {
        var query = _dbContext.Set<FileKnowledgeSource>().AsNoTracking();
        if (userId.HasValue)
            query = query.Where(s => s.CreatedByUserId == userId.Value);

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<int>> GetCompletedKnowledgeOwnerIdsAsync(CancellationToken ct = default)
    {
        return await _dbContext.KnowledgeSources
            .AsNoTracking()
            .Where(s => s.Status == ProcessingStatus.Completed)
            .Select(s => s.CreatedByUserId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<int> GetPendingFileSourceCountAsync(CancellationToken ct = default)
    {
        return await _dbContext.Set<FileKnowledgeSource>()
            .AsNoTracking()
            .CountAsync(s => s.Status == ProcessingStatus.Pending, ct);
    }

    public async Task<List<int>> GetCompletedVectorProfileIdsAsync(CancellationToken ct = default)
    {
        var sources = await _dbContext.KnowledgeSources
            .AsNoTracking()
            .Where(s => s.Status == ProcessingStatus.Completed)
            .ToListAsync(ct);

        return sources
            .Select(KnowledgeVectorProfile.ResolveProfileId)
            .Distinct()
            .ToList();
    }

    public async Task DeleteSourceByIdAsync(int sourceId, CancellationToken ct = default)
    {
        var source = await _dbContext.KnowledgeSources
            .Include(s => s.Chunks)
            .FirstOrDefaultAsync(s => s.Id == sourceId, ct);

        if (source == null)
            return;

        _dbContext.ContentChunks.RemoveRange(source.Chunks);
        _dbContext.KnowledgeSources.Remove(source);
    }

    public async Task ClearChunksForSourceAsync(int sourceId, CancellationToken ct = default)
    {
        var chunks = await _dbContext.ContentChunks
            .Where(c => c.SourceId == sourceId)
            .ToListAsync(ct);

        _dbContext.ContentChunks.RemoveRange(chunks);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return _dbContext.SaveChangesAsync(ct);
    }
}