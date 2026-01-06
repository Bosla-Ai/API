using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public class TrackConfigurations : IEntityTypeConfiguration<Track>
{
    public void Configure(EntityTypeBuilder<Track> builder)
    {
        builder.HasKey(t => t.Id);
        
        // Properties 
        builder.Property(t => t.Title).HasMaxLength(100).IsRequired();

        // Relationships
        builder.HasMany(t => t.Sections)
            .WithOne(s => s.Track)
            .HasForeignKey(s => s.TrackId)
            .OnDelete(DeleteBehavior.Cascade); // Delete Track -> Delete Sections
    }
}