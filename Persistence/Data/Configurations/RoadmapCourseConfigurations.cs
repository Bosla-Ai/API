using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public class RoadmapCourseConfigurations : IEntityTypeConfiguration<RoadmapCourse>
{
    public void Configure(EntityTypeBuilder<RoadmapCourse> builder)
    {
        // Define composite primary key for the many-to-many join table
        builder.HasKey(rc => new { rc.RoadmapId, rc.CourseId });

        // Configure relationship to Roadmap
        builder.HasOne(rc => rc.Roadmap)
            .WithMany(r => r.RoadmapCourses)
            .HasForeignKey(rc => rc.RoadmapId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure relationship to Course
        builder.HasOne(rc => rc.Course)
            .WithMany(c => c.RoadmapCourses)
            .HasForeignKey(rc => rc.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}