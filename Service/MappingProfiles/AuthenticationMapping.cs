using AutoMapper;
using Domain.Entities;
using Domain.Responses;
using Shared.DTOs.ApplicationUserDTOs;

namespace Service.MappingProfiles;

public sealed class AuthenticationMapping : Profile
{
    public AuthenticationMapping()
    {
        CreateMap<ApplicationUserDTO, ApplicationUser>().ReverseMap();
        CreateMap<LoginServerResponse, LoginClientResponse>().ReverseMap();
    }
}