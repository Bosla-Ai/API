using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public class RoadmapCourseConfigurations : IEntityTypeConfiguration<RoadmapCourse>
{
    public void Configure(EntityTypeBuilder<RoadmapCourse> builder)
    {
        builder.HasKey(rc => new { rc.RoadmapId, rc.CourseId });

        // Properties
        builder.Property(rc => rc.Order)
            .IsRequired();

        builder.Property(rc => rc.SectionName)
            .HasMaxLength(200);

        builder.Property(rc => rc.IsCompleted)
            .HasDefaultValue(false);

        builder.Property(rc => rc.CompletedAt)
            .IsRequired(false);

        // Relationships
        builder.HasOne(rc => rc.Roadmap)
            .WithMany(r => r.RoadmapCourses)
            .HasForeignKey(rc => rc.RoadmapId);

        builder.HasOne(rc => rc.Course)
            .WithMany(c => c.RoadmapCourses)
            .HasForeignKey(rc => rc.CourseId);
    }
}