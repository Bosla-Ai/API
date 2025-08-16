using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public class TrackTechnologyConfigurations : IEntityTypeConfiguration<TrackTechnology>
{
    public void Configure(EntityTypeBuilder<TrackTechnology> builder)
    {
        builder.HasKey(k => new { k.TechnologyId, k.TrackId }); // Composite PK
    }
}