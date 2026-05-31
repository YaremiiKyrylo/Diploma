using AIChatAssistant.Domain.Entities;
using AIChatAssistant.Domain.Entities.AiServiceDbEntities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AIChatAssistant.Infrastructure.Persistence.AiServiceDbContext;

public class AiServiceDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, int>
{
    public AiServiceDbContext(DbContextOptions<AiServiceDbContext> options)
        : base(options)
    { }

    public DbSet<KnowledgeSource> KnowledgeSources { get; set; } = null!;
    public DbSet<ContentChunk> ContentChunks { get; set; } = null!;
    public DbSet<FileKnowledgeSource> FileKnowledgeSources { get; set; } = null!;
    public DbSet<TextKnowledgeSource> TextKnowledgeSources { get; set; } = null!;
    public DbSet<ChatSession> ChatSessions { get; set; } = null!;
    public DbSet<ChatMessage> ChatMessages { get; set; } = null!;
    public DbSet<UserChatSettings> UserChatSettings { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AiServiceDbContext).Assembly);
    }
}