using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public class CourseConfigurations : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.HasKey(c => c.Id);

        // Unique Constraints
        builder.HasIndex(c => c.Url).IsUnique();

        // Properties
        builder.Property(c => c.Title)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasMaxLength(1000);

        builder.Property(c => c.Instructor)
            .HasMaxLength(200);

        builder.Property(c => c.ImageUrl)
            .HasMaxLength(1000);

        builder.Property(c => c.Duration)
            .HasMaxLength(50);

        builder.Property(c => c.Rating)
            .HasDefaultValue(0.0);

        // Enums
        builder.Property(c => c.Platform)
            .HasConversion<string>(); // Store as "Udemy", "Coursera"

        builder.Property(c => c.Difficulty)
            .HasConversion<string>(); // Store as "Beginner", etc.

        builder.Property(c => c.CourseBudget)
            .HasConversion<string>();

        builder.Property(c => c.Url)
            .IsRequired()
            .HasMaxLength(1000);

        // Relations
        builder.HasMany(c => c.RoadmapCourses)
            .WithOne(rc => rc.Course)
            .HasForeignKey(rc => rc.CourseId)
            .OnDelete(DeleteBehavior.Restrict); // Safety: Don't delete history if a course changes
    }
}