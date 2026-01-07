using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications.Administration.DomainSpecifications;

public class DomainByIdSpecifications : Specifications<Domains>
{
    public DomainByIdSpecifications(int id)
        : base(d => d.Id == id)
    {
    }
}