using AIChatAssistant.Domain.Entities.AiServiceDbEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIChatAssistant.Infrastructure.DbEntityConfiguration;

public class UserChatSettingsConfig : IEntityTypeConfiguration<UserChatSettings>
{
    public void Configure(EntityTypeBuilder<UserChatSettings> builder)
    {
        builder.ToTable("UserChatSettings");
        builder.HasKey(s => s.UserId);

        builder.Property(s => s.CustomSystemPrompt).IsRequired();

        builder.HasOne(s => s.User)
            .WithOne(u => u.ChatSettings)
            .HasForeignKey<UserChatSettings>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
