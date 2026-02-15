using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications.Administration.TrackSectionSpecifications;

public class TrackSectionByTrackIdSpecification(int trackId) : Specifications<TrackSection>(ts => ts.TrackId == trackId)
{
}