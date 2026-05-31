using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AIChatAssistant.Domain.Entities;

namespace AIChatAssistant.Infrastructure.DbEntityConfiguration;

public class FileKnowledgeSourceConfig : IEntityTypeConfiguration<FileKnowledgeSource>
{
    public void Configure(EntityTypeBuilder<FileKnowledgeSource> builder)
    {
        builder.ToTable("FileKnowledgeSources");

        builder.Property(f => f.FileName).HasMaxLength(500).IsRequired();
        builder.Property(f => f.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(f => f.BlobUrl).HasMaxLength(2000);
    }
}