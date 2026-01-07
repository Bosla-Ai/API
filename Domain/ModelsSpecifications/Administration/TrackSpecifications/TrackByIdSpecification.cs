using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications.Administration.TrackSpecifications;

public class TrackByIdSpecification : Specifications<Track>
{
    public TrackByIdSpecification(int id)
        : base(t => t.Id == id)
    {
    }
}