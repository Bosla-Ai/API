namespace Domain.Entities;

public sealed class CourseTag
{
    public int CourseId { get; set; }
    public Course? Course { get; set; }

    public int TagId { get; set; }
    public Tag? Tag { get; set; }
}