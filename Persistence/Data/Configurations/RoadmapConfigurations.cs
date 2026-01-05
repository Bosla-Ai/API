using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public class RoadmapConfigurations : IEntityTypeConfiguration<Roadmap>
{
    public void Configure(EntityTypeBuilder<Roadmap> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasMaxLength(1000); // Increased for LLM summaries

        builder.Property(r => r.SourceType)
            .HasConversion<string>();

        builder.Property(r => r.TargetJobRole)
            .HasMaxLength(200);

        // Relations
        builder.HasOne(r => r.Customer)
            .WithMany(c => c.RoadMaps)
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}