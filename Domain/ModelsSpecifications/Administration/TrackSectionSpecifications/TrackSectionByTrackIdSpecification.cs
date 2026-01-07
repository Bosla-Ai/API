using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications.Administration.TrackSectionSpecifications;

public class TrackSectionByTrackIdSpecification : Specifications<TrackSection>
{
    public TrackSectionByTrackIdSpecification(int trackId)
        : base(ts => ts.TrackId == trackId)
    {
    }
}