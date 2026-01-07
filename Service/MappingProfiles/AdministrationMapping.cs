using AutoMapper;
using Domain.Entities;
using Shared.DTOs.AdministrationDTOs.TrackSectionDTOs;
using Shared.DTOs.AdministrationDTOs.DomainDTOs;
using Shared.DTOs.AdministrationDTOs.TrackDTOs;
using Shared.DTOs.AdministrationDTOs.TrackChoiceDTOs;

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
        CreateMap<TrackSectionCreateDTO, TrackSection>().ReverseMap();
        CreateMap<TrackSectionUpdateDTO, TrackSection>().ReverseMap();

        CreateMap<TrackChoiceDTO, TrackChoice>().ReverseMap();
        CreateMap<TrackChoiceCreateDTO, TrackChoice>().ReverseMap();
        CreateMap<TrackChoiceUpdateDTO, TrackChoice>().ReverseMap();
    }
}