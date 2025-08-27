using Domain.Entities;
using Shared.DTOs.CustomerDTOs;

namespace Service.Abstraction;

public interface ICustomerService
{
    Task<IEnumerable<Customer>> GetAllAsync();
    Task<Customer> GetByIdAsync(string id);
    Task CreateAsync(Customer customer);
    Task UpdateAsync(Customer customer);
    Task DeleteAsync(Customer customer);
    Task<CustomerDTO> GetALlCustomerDetailsAsync(string id);
}