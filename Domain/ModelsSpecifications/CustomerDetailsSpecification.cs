using System.Linq.Expressions;
using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications;

public class CustomerDetailsSpecification : Specifications<Customer>
{
    public CustomerDetailsSpecification(string customerId)
        : base(c => customerId == c.ApplicationUserId)
    {
        AddInclude(c => c.ApplicationUser);
    }
}