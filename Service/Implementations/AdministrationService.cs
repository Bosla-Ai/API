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
}