using AutoMapper;
using Domain.Entities;
using Shared.DTOs.ApplicationUserDTOs;

namespace Service.MappingProfiles;

public sealed class AuthenticationMapping : Profile
{
    public AuthenticationMapping()
    {
        CreateMap<ApplicationUserDTO, ApplicationUser>().ReverseMap();
    }
}