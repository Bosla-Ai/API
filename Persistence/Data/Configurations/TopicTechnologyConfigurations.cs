using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public class TopicTechnologyConfigurations : IEntityTypeConfiguration<TopicTechnology>
{
    public void Configure(EntityTypeBuilder<TopicTechnology> builder)
    {
        builder.HasKey(t => new { t.TechnologyId, t.TopicId }); 
    }
}