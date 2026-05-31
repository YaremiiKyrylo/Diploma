using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AIChatAssistant.Domain.Entities.AiServiceDbEntities;

namespace AIChatAssistant.Infrastructure.DbEntityConfiguration;

public class ChatSessionConfig : IEntityTypeConfiguration<ChatSession>
{
    public void Configure(EntityTypeBuilder<ChatSession> builder)
    {
        builder.ToTable("ChatSessions");
        builder.HasKey(s => s.Id);

        builder.HasOne(s => s.User)
               .WithMany(u => u.ChatSessions)
               .HasForeignKey(s => s.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.UserId);
    }
}