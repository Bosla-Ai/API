using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications.Administration.DomainSpecifications;

public class DomainsIsActiveSpecifications : Specifications<Domains>
{
    public DomainsIsActiveSpecifications(bool isActive)
        : base(d => d.IsActive == isActive)
    {
    }
}