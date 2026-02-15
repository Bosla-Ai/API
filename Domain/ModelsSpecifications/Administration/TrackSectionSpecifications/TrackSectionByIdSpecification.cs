using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications.Administration.TrackSectionSpecifications;

public class TrackSectionByIdSpecification(int id) : Specifications<TrackSection>(ts => ts.Id == id)
{
}