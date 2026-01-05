using System.Linq.Expressions;
using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications;

public class CourseByUrlSpecification : Specifications<Course>
{
    public CourseByUrlSpecification(string url)
        : base(c => c.Url == url)
    {

    }
}