using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AIChatAssistant.Domain.Entities.AiServiceDbEntities;

namespace AIChatAssistant.Infrastructure.DbEntityConfiguration;

public class ChatMessageConfig : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");
        builder.HasKey(m => m.Id);

        builder.HasOne(m => m.Session)
               .WithMany(s => s.Messages)
               .HasForeignKey(m => m.SessionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.SessionId, m.CreatedAt });
    }
}