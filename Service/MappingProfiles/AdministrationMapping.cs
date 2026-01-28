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

        CreateMap<TrackChoiceDTO, TrackChoice>()
            .ForMember(dest => dest.Label, opt => opt.MapFrom(src => src.Title))
            .ReverseMap()
            .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Label));
        CreateMap<TrackChoiceCreateDTO, TrackChoice>()
            .ForMember(dest => dest.Label, opt => opt.MapFrom(src => src.Title));
        CreateMap<TrackChoiceUpdateDTO, TrackChoice>()
            .ForMember(dest => dest.Label, opt => opt.MapFrom(src => src.Title))
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.SectionId, opt => opt.Ignore());

        CreateMap<TrackSectionCreateFullDTO, TrackSection>()
            .ForMember(dest => dest.Choices, opt => opt.MapFrom(src => src.Choices));
        CreateMap<TrackCreateFullDTO, Track>()
            .ForMember(dest => dest.Sections, opt => opt.MapFrom(src => src.Sections));

        CreateMap<TrackSectionUpdateFullDTO, TrackSection>()
            .ForMember(dest => dest.Choices, opt => opt.Ignore())
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TrackId, opt => opt.Ignore());
        CreateMap<TrackUpdateFullDTO, Track>()
            .ForMember(dest => dest.Sections, opt => opt.Ignore())
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.DomainId, opt => opt.Condition(src => src.DomainId != null));

        // Response DTOs (Entity -> DTO)
        CreateMap<TrackSection, TrackSectionFullDTO>()
            .ForMember(dest => dest.Choices, opt => opt.MapFrom(src => src.Choices));
        CreateMap<Track, TrackFullDTO>()
            .ForMember(dest => dest.Sections, opt => opt.MapFrom(src => src.Sections));
    }
}