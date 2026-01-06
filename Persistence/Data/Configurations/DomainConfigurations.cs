using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public class DomainConfigurations : IEntityTypeConfiguration<Domains>
{
    public void Configure(EntityTypeBuilder<Domains> builder)
    {
        builder.HasKey(d => d.Id);
        
        // Properties
        builder.Property(d => d.Title)
            .HasMaxLength(100)
            .IsRequired();
        
        // Relationships
        builder.HasMany(d => d.Tracks)
            .WithOne(t => t.Domains)
            .HasForeignKey(t => t.DomainId)
            .OnDelete(DeleteBehavior.Cascade); // Delete Domain -> Delete Tracks
    }
}