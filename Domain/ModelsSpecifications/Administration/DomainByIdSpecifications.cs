using System.Linq.Expressions;
using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications.Administration;

public class DomainByIdSpecifications : Specifications<Domains>
{
    public DomainByIdSpecifications(int id)
        : base(d => d.Id == id)
    {
    }
}