using AutoMapper;
using Domain.Entities;
using Shared.DTOs.AdministrationDTOs;
using Shared.DTOs.AdministrationDTOs.DomainDTOs;

namespace Service.MappingProfiles;

public class AdministrationMapping : Profile
{
    public AdministrationMapping()
    {
        CreateMap<DomainsDTO, Domains>().ReverseMap();
        CreateMap<DomainCreateDTO, Domains>().ReverseMap();
        CreateMap<DomainUpdateDTO, Domains>().ReverseMap();

        CreateMap<TrackDTO, Track>().ReverseMap();

        CreateMap<TrackSectionDTO, TrackSection>().ReverseMap();

        CreateMap<TrackChoiceDTO, TrackChoice>().ReverseMap();
    }
}