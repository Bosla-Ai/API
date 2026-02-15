using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications.Administration.TrackSpecifications;

public class TrackByIdSpecification(int id) : Specifications<Track>(t => t.Id == id)
{
}