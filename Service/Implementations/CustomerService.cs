using Domain.Contracts;
using Domain.Entities;
using Service.Abstraction;

namespace Service.Implementations;

public class CustomerService : ICustomerService
{
    private readonly IUnitOfWork _unitOfWork;

    public CustomerService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    
    public async Task<IEnumerable<Customer>> GetAllAsync()
    {
        return await _unitOfWork.GetRepo<Customer, Guid>().GetAllAsync();
    }

    public async Task<Customer> GetByIdAsync(Guid id)
    {
        return await _unitOfWork.GetRepo<Customer, Guid>().GetIdAsync(id);
    }

    public async Task CreateAsync(Customer customer)
    {
        await _unitOfWork.GetRepo<Customer, Guid>().CreateAsync(customer);
    }

    public async Task UpdateAsync(Customer customer)
    {
        await _unitOfWork.GetRepo<Customer, Guid>().UpdateAsync(customer);
    }

    public async Task DeleteAsync(Customer customer)
    {
        await _unitOfWork.GetRepo<Customer, Guid>().DeleteAsync(customer);
    }
}