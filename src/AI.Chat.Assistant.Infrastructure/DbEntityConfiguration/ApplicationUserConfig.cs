using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AIChatAssistant.Domain.Entities;

namespace AIChatAssistant.Infrastructure.DbEntityConfiguration;

public class ApplicationUserConfig : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.CreatedAt)
               .HasDefaultValueSql("GETUTCDATE()"); 
    }
}