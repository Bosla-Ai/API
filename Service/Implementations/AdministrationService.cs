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
        if (id <= 0 || id == null)
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

    public async Task<APIResponse<int>> AddDomain(DomainCreateDTO domainsDto)
    {
        if (domainsDto == null)
            throw new BadRequestException("invalid domain details");

        var domain = mapper.Map<Domains>(domainsDto);
        await unitOfWork.GetRepo<Domains, int>().CreateAsync(domain);
        await unitOfWork.SaveChangesAsync();

        return new APIResponse<int>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = domain.Id,
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
        if (id <= 0 || id == null)
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
        if (domainId <= 0)
            throw new BadRequestException("Invalid domain ID");

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





    public async Task<APIResponse<int>> AddTrackFull(TrackCreateFullDTO trackDto)
    {
        if (trackDto == null)
            throw new BadRequestException("invalid track details");

        var track = mapper.Map<Track>(trackDto);

        await unitOfWork.GetRepo<Track, int>().CreateAsync(track);
        await unitOfWork.SaveChangesAsync();

        return new APIResponse<int>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = track.Id,
        };
    }

    public async Task<APIResponse<TrackFullDTO>> GetFullTrack(int id)
    {
        if (id <= 0)
            throw new BadRequestException("Invalid track ID");

        var spec = new TrackWithFullStructureSpecification(id);
        var track = await unitOfWork.GetRepo<Track, int>().GetAsync(spec);
        if (track == null)
            throw new NotFoundException("This No Track Match This Id !!");

        return new APIResponse<TrackFullDTO>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = mapper.Map<TrackFullDTO>(track)
        };
    }

    public async Task<APIResponse> UpdateFullTrack(TrackUpdateFullDTO trackDto)
    {
        if (trackDto == null)
            throw new BadRequestException("invalid track details");

        var spec = new TrackWithFullStructureSpecification(trackDto.Id);
        var existingTrack = await unitOfWork.GetRepo<Track, int>().GetAsync(spec);

        if (existingTrack == null)
            throw new NotFoundException("Track not found");

        mapper.Map(trackDto, existingTrack);

        if (trackDto.Sections != null)
        {
            foreach (var sectionDto in trackDto.Sections)
            {
                if (sectionDto.Id == 0)
                {
                    var newSection = mapper.Map<TrackSection>(sectionDto);
                    if (sectionDto.Choices != null)
                    {
                        newSection.Choices = mapper.Map<ICollection<TrackChoice>>(sectionDto.Choices);
                    }

                    if (existingTrack.Sections == null)
                        existingTrack.Sections = new List<TrackSection>();

                    existingTrack.Sections.Add(newSection);
                }
                else if (sectionDto.Id > 0)
                {
                    var existingSection = existingTrack.Sections?.FirstOrDefault(s => s.Id == sectionDto.Id);
                    if (existingSection != null)
                    {
                        mapper.Map(sectionDto, existingSection);

                        if (sectionDto.Choices != null)
                        {
                            foreach (var choiceDto in sectionDto.Choices)
                            {
                                if (choiceDto.Id == 0)
                                {
                                    var newChoice = mapper.Map<TrackChoice>(choiceDto);
                                    if (existingSection.Choices == null) existingSection.Choices = new List<TrackChoice>();
                                    existingSection.Choices.Add(newChoice);
                                }
                                else if (choiceDto.Id > 0)
                                {
                                    var existingChoice = existingSection.Choices?.FirstOrDefault(c => c.Id == choiceDto.Id);
                                    if (existingChoice != null)
                                    {
                                        mapper.Map(choiceDto, existingChoice);
                                        // Just for ensuring the section id is correct
                                        existingChoice.SectionId = existingSection.Id;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        await unitOfWork.GetRepo<Track, int>().UpdateAsync(existingTrack);
        await unitOfWork.SaveChangesAsync();

        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse> DeleteTrack(int id)
    {
        if (id <= 0 || id == null)
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

    public async Task<APIResponse> DeleteSection(int id)
    {
        if (id <= 0 || id == null)
            throw new BadRequestException("invalid section id");

        var section = new TrackSectionByIdSpecification(id);
        var existingSection = await unitOfWork
            .GetRepo<TrackSection, int>().GetAsync(section);

        if (existingSection == null)
            throw new NotFoundException("Section not found");

        await unitOfWork.GetRepo<TrackSection, int>().DeleteAsync(existingSection);
        await unitOfWork.SaveChangesAsync();

        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse> DeleteChoice(int id)
    {
        if (id <= 0 || id == null)
            throw new BadRequestException("id is 0 or null");

        var choice = new TrackChoiceByIdSpecification(id);
        var existingChoice = await unitOfWork
            .GetRepo<TrackChoice, int>().GetAsync(choice);

        if (existingChoice == null)
            throw new NotFoundException("Choice not found");

        await unitOfWork.GetRepo<TrackChoice, int>().DeleteAsync(existingChoice);
        await unitOfWork.SaveChangesAsync();

        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }
}