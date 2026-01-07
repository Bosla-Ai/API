using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications.Administration.TrackSectionSpecifications;

public class TrackSectionByIdSpecification : Specifications<TrackSection>
{
    public TrackSectionByIdSpecification(int id)
        : base(ts => ts.Id == id)
    {
    }
}