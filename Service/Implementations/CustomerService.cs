using AutoMapper;
using Domain.Contracts;
using Domain.Entities;
using Domain.ModelsSpecifications;
using Service.Abstraction;
using Shared.DTOs.CustomerDTOs;

namespace Service.Implementations;

public class CustomerService : ICustomerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CustomerService(
        IUnitOfWork unitOfWork
        ,IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }
    
    public async Task<IEnumerable<Customer>> GetAllAsync()
    {
        return await _unitOfWork.GetRepo<Customer, string>().GetAllAsync();
    }

    public async Task<Customer> GetByIdAsync(string id)
    {
        return await _unitOfWork.GetRepo<Customer, string>().GetIdAsync(id);
    }

    public async Task CreateAsync(Customer customer)
    {
        await _unitOfWork.GetRepo<Customer, string>().CreateAsync(customer);
    }

    public async Task UpdateAsync(Customer customer)
    {
        await _unitOfWork.GetRepo<Customer, string>().UpdateAsync(customer);
    }

    public async Task DeleteAsync(Customer customer)
    {
        await _unitOfWork.GetRepo<Customer, string>().DeleteAsync(customer);
    }

    public async Task<CustomerDTO> GetALlCustomerDetailsAsync(string id)
    {
        var customer = await _unitOfWork.GetRepo<Customer, string>()
            .GetAsync(new CustomerDetailsSpecification(id));
        
        return _mapper.Map<CustomerDTO>(customer);
    }
}