using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AIChatAssistant.Domain.Entities;

namespace AIChatAssistant.Infrastructure.DbEntityConfiguration;

public class KnowledgeSourceConfig : IEntityTypeConfiguration<KnowledgeSource>
{
    public void Configure(EntityTypeBuilder<KnowledgeSource> builder)
    {
        builder.ToTable("KnowledgeSources");
        builder.HasKey(k => k.Id);

        builder.HasOne(k => k.Author)
               .WithMany(u => u.UploadedSources)
               .HasForeignKey(k => k.CreatedByUserId)
               .OnDelete(DeleteBehavior.Restrict); 

        builder.HasIndex(k => k.CreatedByUserId);
    }
}