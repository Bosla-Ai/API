using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications.Administration.DomainSpecifications;

public class DomainByIdSpecifications(int id) : Specifications<Domains>(d => d.Id == id)
{
}