using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications.Administration.TrackChoiceSpecifications;

public class TrackChoiceByIdSpecification(int id) : Specifications<TrackChoice>(tc => tc.Id == id)
{
}