using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public class RoadMapResourceConfigurations : IEntityTypeConfiguration<RoadmapResource>
{
    public void Configure(EntityTypeBuilder<RoadmapResource> builder)
    {
        builder.ToTable("RoadmapResources");
        builder.HasKey(k=> new { k.ResourceId, k.RoadmapId });
    }
}