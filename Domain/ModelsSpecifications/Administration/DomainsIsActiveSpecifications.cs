using System.Linq.Expressions;
using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications.Administration;

public class DomainsIsActiveSpecifications : Specifications<Domains>
{
    public DomainsIsActiveSpecifications(bool isActive)
        : base(d => d.IsActive == isActive)
    {
    }
}