using AutoMapper;
using Domain.Entities;
using Shared.DTOs.ApplicationUserDTOs;
using Shared.DTOs.CustomerDTOs;
using Shared.DTOs.RegisterDTOs;

namespace Service.MappingProfiles;

public sealed class CustomerMapping : Profile
{
    public CustomerMapping()
    {
        CreateMap<Customer, CustomerRegisterDTO>().ReverseMap();
        CreateMap<Customer, CustomerDTO>().ReverseMap();
        CreateMap<ApplicationUser, ApplicationUserDTO>().ReverseMap();

        CreateMap<Customer, CustomerCreateDTO>().ReverseMap();
        CreateMap<Customer, CustomerUpdateDTO>().ReverseMap();

        CreateMap<ApplicationUser, CustomerRegisterDTO>().ReverseMap();
    }
}