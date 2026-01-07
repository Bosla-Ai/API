using AutoMapper;
using Domain.Entities;
using Shared.DTOs.AdministrationDTOs;
using Shared.DTOs.AdministrationDTOs.DomainDTOs;
using Shared.DTOs.AdministrationDTOs.TrackDTOs;

namespace Service.MappingProfiles;

public class AdministrationMapping : Profile
{
    public AdministrationMapping()
    {
        CreateMap<DomainsDTO, Domains>().ReverseMap();
        CreateMap<DomainCreateDTO, Domains>().ReverseMap();
        CreateMap<DomainUpdateDTO, Domains>().ReverseMap();

        CreateMap<TrackDTO, Track>().ReverseMap();
        CreateMap<TrackCreateDTO, Track>().ReverseMap();
        CreateMap<TrackUpdateDTO, Track>().ReverseMap();

        CreateMap<TrackSectionDTO, TrackSection>().ReverseMap();

        CreateMap<TrackChoiceDTO, TrackChoice>().ReverseMap();
    }
}