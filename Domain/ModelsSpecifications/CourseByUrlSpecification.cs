using System.Linq.Expressions;
using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications;

public class CourseByUrlSpecification(string url) : Specifications<Course>(c => c.Url == url)
{
}