using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public class TrackSectionConfigurations : IEntityTypeConfiguration<TrackSection>
{
    public void Configure(EntityTypeBuilder<TrackSection> builder)
    {
        builder.HasKey(s => s.Id);

        // Properties
        builder.Property(s => s.Title)
            .HasMaxLength(100)
            .IsRequired();

        // Relationships
        builder.HasMany(s => s.Choices)
            .WithOne(c => c.Section)
            .HasForeignKey(c => c.SectionId)
            .OnDelete(DeleteBehavior.Cascade); // Delete Section -> Delete Choices
    }
}