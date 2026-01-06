using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public class RefreshTokenConfigurations : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(r => new { r.DeviceId, r.UserId });

        builder.Property(r => r.Created)
            .HasDefaultValueSql("GETDATE()");

        builder.HasOne(r => r.User)
            .WithMany(a => a.RefreshTokens)
            .HasForeignKey(r => r.UserId);
    }
}