using System.Net;
using AutoMapper;
using Domain.Contracts;
using Domain.Exceptions;
using Domain.ModelsSpecifications.Administration;
using Domain.Responses;
using Domain.Entities;
using Service.Abstraction;
using Shared.DTOs.AdministrationDTOs.DomainDTOs;

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
}