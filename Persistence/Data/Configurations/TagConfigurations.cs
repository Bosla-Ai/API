using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public class TagConfigurations : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(t => t.Id);

        // Unique Constraints
        builder.HasIndex(t => t.Name).IsUnique();
        
        // Properties
        builder.Property(t => t.Name)
            .HasMaxLength(50)
            .IsRequired();
    }
}