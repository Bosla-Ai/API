using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public class CourseConfigurations : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.Title)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(c => c.Instructor)
            .HasMaxLength(100);

        builder.Property(c => c.Platform)
            .HasConversion<string>();

        builder.Property(c => c.CourseBudget)
            .HasConversion<string>();

        builder.Property(c => c.Url)
            .IsRequired();
        
        //Relations
        builder.HasMany(c => c.RoadmapCourses)
            .WithOne(rc => rc.Course)
            .HasForeignKey(fk => fk.CourseId);

        builder.HasMany(c => c.CourseTags)
            .WithOne(ct => ct.Course)
            .HasForeignKey(fk => fk.CourseId);
    }
}