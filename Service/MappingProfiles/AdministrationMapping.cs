using AutoMapper;
using Domain.Entities;
using Shared.DTOs.AdministrationDTOs;

namespace Service.MappingProfiles;

public class AdministrationMapping : Profile
{
    public AdministrationMapping()
    {
        CreateMap<DomainsDTO, Domains>().ReverseMap();
        CreateMap<TrackDTO, Track>().ReverseMap();
        CreateMap<TrackSectionDTO, TrackSection>().ReverseMap();
        CreateMap<TrackChoiceDTO, TrackChoice>().ReverseMap();
    }
}