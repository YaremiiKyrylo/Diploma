using Microsoft.EntityFrameworkCore;
using AIChatAssistant.Domain.Entities;
using AIChatAssistant.Domain.RepositoryInterfaces;
using AIChatAssistant.Infrastructure.Persistence.AiServiceDbContext;

namespace AIChatAssistant.Infrastructure.Repositories;

public class AiDataRepository : IAiDataRepository
{
    private readonly AiServiceDbContext _context;

    public AiDataRepository(AiServiceDbContext context)
    {
        _context = context;
    }

    public async Task<KnowledgeSource?> GetByIdAsync(int id)
    {
        return await _context.KnowledgeSources
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<List<KnowledgeSource>> GetByProfileIdAsync(int profileId)
    {
        var content = await _context.KnowledgeSources
            .AsNoTracking()
            .Where(d => d.CreatedByUserId == profileId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        return content; 
    }

    public async Task<List<KnowledgeSource>> GetPendingDocumentsAsync()
    {
        return await _context.KnowledgeSources
            .AsNoTracking()
            .Where(d => d.Status == ProcessingStatus.Pending)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<KnowledgeSource>> GetByProfileAndStatusAsync(
        int profileId,
        ProcessingStatus status)
    {
        return await _context.KnowledgeSources
            .AsNoTracking()
            .Where(d => d.CreatedByUserId == profileId && d.Status == status)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(KnowledgeSource document)
    {
        await _context.KnowledgeSources.AddAsync(document);
    }

    public void Remove(KnowledgeSource document)
    {
        _context.KnowledgeSources.Remove(document);
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.KnowledgeSources.AnyAsync(d => d.Id == id);
    }

    public async Task<int> GetDocumentCountByProfileAsync(int profileId)
    {
        return await _context.KnowledgeSources
            .CountAsync(d => d.CreatedByUserId == profileId);
    }

    public async Task<(List<KnowledgeSource> Items, int TotalCount)> GetPagedByProfileAsync(
        int profileId,
        int pageNumber,
        int pageSize)
    {
        var query = _context.KnowledgeSources
            .AsNoTracking()
            .Where(d => d.CreatedByUserId == profileId);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}