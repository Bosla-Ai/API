using System.Net;
using AutoMapper;
using Domain.Contracts;
using Domain.Exceptions;
using Domain.ModelsSpecifications.Administration;
using Domain.Responses;
using Domain.Entities;
using Service.Abstraction;
using Shared.DTOs.AdministrationDTOs;

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

    public async Task<APIResponse> AddDomain(DomainsDTO domainsDto)
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
}