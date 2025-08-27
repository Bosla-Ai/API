using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public sealed class ResourceConfigurations : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Level)
            .HasConversion<string>();
        builder.Property(r => r.CostType)
            .HasConversion<string>();
        builder.Property(r => r.ResourceType)
            .HasConversion<string>();
        builder.Property(r => r.LearningStyle)
            .HasConversion<string>();
        builder.Property(r => r.ResourceLanguage)
            .HasConversion<string>();
    }
}