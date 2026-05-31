using AIChatAssistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIChatAssistant.Infrastructure.DbEntityConfiguration;

public class TextKnowledgeSourceConfig : IEntityTypeConfiguration<TextKnowledgeSource>
{
    public void Configure(EntityTypeBuilder<TextKnowledgeSource> builder)
    {
        builder.ToTable("TextKnowledgeSources");
    }
}