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

        builder.Property(rc => rc.CurrentPositionSeconds)
            .HasDefaultValue(0);

        builder.Property(rc => rc.TotalDurationSeconds)
            .HasDefaultValue(0);

        builder.Property(rc => rc.VideoId)
            .HasMaxLength(20)
            .IsRequired(false);

        // Relationships
        builder.HasOne(rc => rc.Roadmap)
            .WithMany(r => r.RoadmapCourses)
            .HasForeignKey(rc => rc.RoadmapId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rc => rc.Course)
            .WithMany(c => c.RoadmapCourses)
            .HasForeignKey(rc => rc.CourseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}