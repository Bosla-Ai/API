using System.Linq.Expressions;
using Domain.Contracts;
using Domain.Entities;
using Shared.Enums;

namespace Domain.ModelsSpecifications;

public class CoursesByTagsAndLanguageSpecification : Specifications<Course>
{
    public CoursesByTagsAndLanguageSpecification(string[] tags, ResourceLanguage language)
        : base(c => c.Language == language && c.CourseTags.Any(ct => ct.Tag != null && tags.Contains(ct.Tag.Name)))
    {
        AddInclude(c => c.CourseTags);
        AddInclude("CourseTags.Tag");
    }
}
