using System.Linq.Expressions;
using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications.Administration.TrackSpecifications;

public class TracksByDomainIdSpecification : Specifications<Track>
{
    public TracksByDomainIdSpecification(int domainId)
        : base(t => t.DomainId == domainId)
    {
    }
}