using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public class TrackChoiceConfigurations : IEntityTypeConfiguration<TrackChoice>
{
    public void Configure(EntityTypeBuilder<TrackChoice> builder)
    {
        builder.HasKey(c => c.Id);
        
        // Properties
        builder.Property(c => c.Label)
            .HasMaxLength(100)
            .IsRequired();
        
        builder.Property(c => c.IsDefault)
            .HasDefaultValue(false);
    }
}