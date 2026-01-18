using AutoMapper;
using Domain.Entities;
using Domain.Responses;
using Shared.DTOs.ApplicationUserDTOs;

namespace Service.MappingProfiles;

public sealed class AuthenticationMapping : Profile
{
    public AuthenticationMapping()
    {
        CreateMap<ApplicationUser, ApplicationUserDTO>()
            .ForMember(dest => dest.Role, opt => opt.Ignore());
        CreateMap<ApplicationUserDTO, ApplicationUser>();
        CreateMap<LoginServerResponse, LoginClientResponse>().ReverseMap();
    }
}