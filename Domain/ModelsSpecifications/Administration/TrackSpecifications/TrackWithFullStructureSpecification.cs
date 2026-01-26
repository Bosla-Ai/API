using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications.Administration.TrackSpecifications;

public class TrackWithFullStructureSpecification : Specifications<Track>
{
    public TrackWithFullStructureSpecification(int id)
        : base(t => t.Id == id)
    {
        AddInclude(t => t.Sections);
        AddInclude("Sections.Choices");
    }
}
