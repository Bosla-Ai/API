using System.Net;
using AutoMapper;
using Domain.Contracts;
using Domain.Exceptions;
using Domain.ModelsSpecifications.Administration.DomainSpecifications;
using Domain.ModelsSpecifications.Administration.TrackSpecifications;
using Domain.Responses;
using Domain.Entities;
using Domain.ModelsSpecifications.Administration.TrackChoiceSpecifications;
using Domain.ModelsSpecifications.Administration.TrackSectionSpecifications;
using Service.Abstraction;
using Shared.DTOs.AdministrationDTOs.DomainDTOs;
using Shared.DTOs.AdministrationDTOs.TrackChoiceDTOs;
using Shared.DTOs.AdministrationDTOs.TrackDTOs;
using Shared.DTOs.AdministrationDTOs.TrackSectionDTOs;

namespace Service.Implementations;

public class AdministrationService(
    IUnitOfWork unitOfWork,
    IMapper mapper) : IAdministrationService
{
    public async Task<APIResponse<IEnumerable<DomainsDTO>>> GetDomainsAsync(bool isActive)
    {
        var spec = new DomainsIsActiveSpecifications(isActive);
        var domains = await unitOfWork.GetRepo<Domains, int>().GetAllAsync(spec);
        if (domains == null)
            throw new NotFoundException("No Domains Exit Right Now");

        return new APIResponse<IEnumerable<DomainsDTO>>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = mapper.Map<IEnumerable<DomainsDTO>>(domains)
        };
    }

    public async Task<APIResponse<DomainsDTO>> GetDomainAsync(int id)
    {
        if (id == 0 || id == null)
            throw new BadRequestException("invalid domain id");

        var spec = new DomainByIdSpecifications(id);
        var domain = await unitOfWork.GetRepo<Domains, int>().GetAsync(spec);
        if (domain == null)
            throw new NotFoundException("This No Domain Match This Id !!");

        return new APIResponse<DomainsDTO>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = mapper.Map<DomainsDTO>(domain)
        };
    }

    public async Task<APIResponse> AddDomain(DomainCreateDTO domainsDto)
    {
        if (domainsDto == null)
            throw new BadRequestException("invalid domain details");

        var domain = mapper.Map<Domains>(domainsDto);
        await unitOfWork.GetRepo<Domains, int>().CreateAsync(domain);
        await unitOfWork.SaveChangesAsync();

        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse> UpdateDomain(DomainUpdateDTO domainsDto)
    {
        if (domainsDto == null)
            throw new BadRequestException("invalid domain details");

        var domain = mapper.Map<Domains>(domainsDto);
        if (domain == null)
            throw new InternalServerErrorException("Internal Server Error While Mapping");

        await unitOfWork.GetRepo<Domains, int>().UpdateAsync(domain);
        await unitOfWork.SaveChangesAsync();

        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse> DeleteDomain(int id)
    {
        if (id == 0 || id == null)
            throw new BadRequestException("invalid domain id");

        var spec = new DomainByIdSpecifications(id);
        var domain = await unitOfWork.GetRepo<Domains, int>().GetAsync(spec);
        if (domain == null)
            throw new NotFoundException("This No Domain Match This Id !!");

        await unitOfWork.GetRepo<Domains, int>().DeleteAsync(domain);
        await unitOfWork.SaveChangesAsync();
        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse<IEnumerable<TrackDTO>>> GetTracks(int domainId)
    {
        var spec = new TracksByDomainIdSpecification(domainId);
        var tracks = await unitOfWork.GetRepo<Track, int>().GetAllAsync(spec);
        if (tracks == null)
            throw new NotFoundException("No Tracks Exit Right Now");

        return new APIResponse<IEnumerable<TrackDTO>>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = mapper.Map<IEnumerable<TrackDTO>>(tracks)
        };
    }

    public async Task<APIResponse<TrackDTO>> GetTrack(int id)
    {
        if (id == 0 || id == null)
            throw new BadRequestException("invalid track id");

        var spec = new TrackByIdSpecification(id);
        var track = await unitOfWork.GetRepo<Track, int>().GetAsync(spec);
        if (track == null)
            throw new NotFoundException("This No Track Match This Id !!");

        return new APIResponse<TrackDTO>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = mapper.Map<TrackDTO>(track)
        };
    }

    public async Task<APIResponse> AddTrack(TrackCreateDTO trackDto)
    {
        if (trackDto == null)
            throw new BadRequestException("invalid track details");

        var track = mapper.Map<Track>(trackDto);
        await unitOfWork.GetRepo<Track, int>().CreateAsync(track);
        await unitOfWork.SaveChangesAsync();
        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse> UpdateTrack(TrackUpdateDTO trackDto)
    {
        if (trackDto == null)
            throw new BadRequestException("invalid track details");

        var track = mapper.Map<Track>(trackDto);
        if (track == null)
            throw new InternalServerErrorException("Internal Server Error While Mapping");

        await unitOfWork.GetRepo<Track, int>().UpdateAsync(track);
        await unitOfWork.SaveChangesAsync();
        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse> DeleteTrack(int id)
    {
        if (id == 0 || id == null)
            throw new BadRequestException("invalid track id");

        var spec = new TrackByIdSpecification(id);
        var track = await unitOfWork.GetRepo<Track, int>().GetAsync(spec);
        if (track == null)
            throw new NotFoundException("This No Track Match This Id !!");

        await unitOfWork.GetRepo<Track, int>().DeleteAsync(track);
        await unitOfWork.SaveChangesAsync();
        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse<IEnumerable<TrackSectionDTO>>> GetTrackSections(int trackId)
    {
        var spec = new TrackSectionByTrackIdSpecification(trackId);
        var trackSections = await unitOfWork.GetRepo<TrackSection, int>().GetAllAsync(spec);
        if (trackSections == null)
            throw new NotFoundException("No Track Sections Exit Right Now");

        return new APIResponse<IEnumerable<TrackSectionDTO>>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = mapper.Map<IEnumerable<TrackSectionDTO>>(trackSections)
        };
    }

    public async Task<APIResponse<TrackSectionDTO>> GetTrackSection(int id)
    {
        if (id == 0 || id == null)
            throw new BadRequestException("invalid track section id");

        var spec = new TrackSectionByIdSpecification(id);
        var trackSection = await unitOfWork.GetRepo<TrackSection, int>().GetAsync(spec);
        if (trackSection == null)
            throw new NotFoundException("This No Track Section Match This Id !!");

        return new APIResponse<TrackSectionDTO>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = mapper.Map<TrackSectionDTO>(trackSection)
        };
    }

    public async Task<APIResponse> AddTrackSection(TrackSectionCreateDTO trackSectionDto)
    {
        if (trackSectionDto == null)
            throw new BadRequestException("invalid track section details");

        var trackSection = mapper.Map<TrackSection>(trackSectionDto);
        await unitOfWork.GetRepo<TrackSection, int>().CreateAsync(trackSection);
        await unitOfWork.SaveChangesAsync();

        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse> UpdateTrackSection(TrackSectionUpdateDTO trackSectionDto)
    {
        if (trackSectionDto == null)
            throw new BadRequestException("invalid track section details");

        var trackSection = mapper.Map<TrackSection>(trackSectionDto);
        if (trackSection == null)
            throw new InternalServerErrorException("Internal Server Error While Mapping");

        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse> DeleteTrackSection(int id)
    {
        if (id == 0 || id == null)
            throw new BadRequestException("invalid track section id");

        var spec = new TrackSectionByIdSpecification(id);
        var trackSection = await unitOfWork.GetRepo<TrackSection, int>().GetAsync(spec);
        if (trackSection == null)
            throw new NotFoundException("This No Track Section Match This Id !!");

        await unitOfWork.GetRepo<TrackSection, int>().DeleteAsync(trackSection);
        await unitOfWork.SaveChangesAsync();

        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse<IEnumerable<TrackChoiceDTO>>> GetTrackChoices(int trackId)
    {
        var spec = new TrackChoiceByTrackSectionIdSpecification(trackId);
        var trackChoices = await unitOfWork.GetRepo<TrackChoice, int>().GetAllAsync(spec);
        if (trackChoices == null)
            throw new NotFoundException("No Track Choices Exit Right Now");

        return new APIResponse<IEnumerable<TrackChoiceDTO>>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = mapper.Map<IEnumerable<TrackChoiceDTO>>(trackChoices)
        };
    }

    public async Task<APIResponse<TrackChoiceDTO>> GetTrackChoice(int id)
    {
        if (id == 0 || id == null)
            throw new BadRequestException("invalid track choice id");

        var spec = new TrackChoiceByIdSpecification(id);
        var trackChoice = await unitOfWork.GetRepo<TrackChoice, int>().GetAsync(spec);
        if (trackChoice == null)
            throw new NotFoundException("This No Track Choice Match This Id");

        return new APIResponse<TrackChoiceDTO>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = mapper.Map<TrackChoiceDTO>(trackChoice)
        };
    }

    public async Task<APIResponse> AddTrackChoice(TrackChoiceCreateDTO trackChoiceDto)
    {
        if (trackChoiceDto == null)
            throw new BadRequestException("invalid track choice details");

        var trackChoice = mapper.Map<TrackChoice>(trackChoiceDto);
        await unitOfWork.GetRepo<TrackChoice, int>().CreateAsync(trackChoice);
        await unitOfWork.SaveChangesAsync();

        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse> UpdateTrackChoice(TrackChoiceUpdateDTO trackChoiceDto)
    {
        if (trackChoiceDto == null)
            throw new BadRequestException("invalid track choice details");

        var trackChoice = mapper.Map<TrackChoice>(trackChoiceDto);
        if (trackChoice == null)
            throw new InternalServerErrorException("Internal Server Error While Mapping");

        await unitOfWork.GetRepo<TrackChoice, int>().UpdateAsync(trackChoice);
        await unitOfWork.SaveChangesAsync();

        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse> DeleteTrackChoice(int id)
    {
        if (id == 0 || id == null)
            throw new BadRequestException("invalid track choice id");

        var spec = new TrackChoiceByIdSpecification(id);
        var trackChoice = await unitOfWork.GetRepo<TrackChoice, int>().GetAsync(spec);
        if (trackChoice == null)
            throw new NotFoundException("This No Track Choice Match This Id");

        await unitOfWork.GetRepo<TrackChoice, int>().DeleteAsync(trackChoice);
        await unitOfWork.SaveChangesAsync();

        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }
}