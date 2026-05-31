using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AIChatAssistant.Domain.Entities;

namespace AIChatAssistant.Infrastructure.DbEntityConfiguration;

public class ContentChunkConfig : IEntityTypeConfiguration<ContentChunk>
{
    public void Configure(EntityTypeBuilder<ContentChunk> builder)
    {
        builder.ToTable("ContentChunks");
        builder.HasKey(c => c.Id);

        builder.HasOne(c => c.Source)
               .WithMany(s => s.Chunks)
               .HasForeignKey(c => c.SourceId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.SourceId);

        builder.HasIndex(c => c.PineconeVectorId).IsUnique();
    }
}