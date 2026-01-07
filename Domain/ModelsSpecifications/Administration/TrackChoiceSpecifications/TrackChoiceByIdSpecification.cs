using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications.Administration.TrackChoiceSpecifications;

public class TrackChoiceByIdSpecification : Specifications<TrackChoice>
{
    public TrackChoiceByIdSpecification(int id)
        : base(tc => tc.Id == id)
    {
    }
}