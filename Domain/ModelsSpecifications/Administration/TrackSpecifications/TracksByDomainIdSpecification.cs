using System.Linq.Expressions;
using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications.Administration.TrackSpecifications;

public class TracksByDomainIdSpecification(int domainId) : Specifications<Track>(t => t.DomainId == domainId)
{
}