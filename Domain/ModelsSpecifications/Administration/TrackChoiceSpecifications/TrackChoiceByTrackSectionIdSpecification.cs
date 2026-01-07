using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications.Administration.TrackChoiceSpecifications;

public class TrackChoiceByTrackSectionIdSpecification : Specifications<TrackChoice>
{
    public TrackChoiceByTrackSectionIdSpecification(int trackSectionId)
        : base(tc => tc.SectionId == trackSectionId)
    {
    }
}