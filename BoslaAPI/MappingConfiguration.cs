using AutoMapper;
using Domain.Entities;
using Shared.DTOs.CustomerDTOs;
using Shared.DTOs.RegisterDTOs;

namespace BoslaAPI;

public class MappingConfiguration :  Profile
{
    public MappingConfiguration()
    {
        CreateMap<Customer, CustomerRegisterDTO>().ReverseMap();
        CreateMap<Customer, CustomerDTO>().ReverseMap();
        CreateMap<Customer, CustomerCreateDTO>().ReverseMap();
        CreateMap<Customer, CustomerUpdateDTO>().ReverseMap();
        
        CreateMap<ApplicationUser, CustomerRegisterDTO>().ReverseMap();
    }
}