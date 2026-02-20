using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications;

public class RoadmapsByCustomerSpecification : Specifications<Roadmap>
{
    public RoadmapsByCustomerSpecification(string customerId)
        : base(r => r.CustomerId == customerId && !r.IsArchived)
    {
    }

    public RoadmapsByCustomerSpecification(int roadmapId, string customerId)
        : base(r => r.Id == roadmapId && r.CustomerId == customerId && !r.IsArchived)
    {
        AddInclude("RoadmapCourses");
        AddInclude("RoadmapCourses.Course");
    }
}
