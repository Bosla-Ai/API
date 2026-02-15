using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications.Administration.DomainSpecifications;

public class DomainsIsActiveSpecifications(bool isActive) : Specifications<Domains>(d => d.IsActive == isActive)
{
}