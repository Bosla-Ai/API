using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications.Administration.TrackChoiceSpecifications;

public class TrackChoiceByTrackSectionIdSpecification(int trackSectionId) : Specifications<TrackChoice>(tc => tc.SectionId == trackSectionId)
{
}