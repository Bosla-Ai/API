using System.Net;
using System.Security.Claims;
using AutoMapper;
using Domain.Contracts;
using Domain.Entities;
using Domain.Exceptions;
using Domain.ModelsSpecifications.Administration.AdminsSpecifications;
using Domain.ModelsSpecifications.Administration.DomainSpecifications;
using Domain.ModelsSpecifications.Administration.TrackChoiceSpecifications;
using Domain.ModelsSpecifications.Administration.TrackSectionSpecifications;
using Domain.ModelsSpecifications.Administration.TrackSpecifications;
using Domain.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Service.Abstraction;
using Shared;
using Shared.DTOs.AdministrationDTOs.AdminDTOs;
using Shared.DTOs.AdministrationDTOs.DomainDTOs;
using Shared.DTOs.AdministrationDTOs.TrackDTOs;

namespace Service.Implementations;

public class AdministrationService(
    IUnitOfWork unitOfWork,
    IMapper mapper,
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor httpContextAccessor) : IAdministrationService
{
    public async Task<APIResponse<IEnumerable<DomainsDTO>>> GetDomainsAsync(bool isActive)
    {
        var spec = new DomainsIsActiveSpecifications(isActive);
        var domains = await unitOfWork.GetRepo<Domains, int>().GetAllAsync(spec) ?? throw new NotFoundException("No Domains Exit Right Now");
        return new APIResponse<IEnumerable<DomainsDTO>>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = mapper.Map<IEnumerable<DomainsDTO>>(domains)
        };
    }

    public async Task<APIResponse<DomainsDTO>> GetDomainAsync(int id)
    {
        if (id <= 0)
            throw new BadRequestException("invalid domain id");

        var spec = new DomainByIdSpecifications(id);
        var domain = await unitOfWork.GetRepo<Domains, int>().GetAsync(spec) ?? throw new NotFoundException("This No Domain Match This Id !!");
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

        var domain = mapper.Map<Domains>(domainsDto) ?? throw new InternalServerErrorException("Internal Server Error While Mapping");
        await unitOfWork.GetRepo<Domains, int>().UpdateAsync(domain);
        await unitOfWork.SaveChangesAsync();

        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse> DeleteDomain(int id)
    {
        if (id <= 0)
            throw new BadRequestException("invalid domain id");

        var spec = new DomainByIdSpecifications(id);
        var domain = await unitOfWork.GetRepo<Domains, int>().GetAsync(spec) ?? throw new NotFoundException("This No Domain Match This Id !!");
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
        var tracks = await unitOfWork.GetRepo<Track, int>().GetAllAsync(spec) ?? throw new NotFoundException("No Tracks Exit Right Now");
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
        var track = await unitOfWork.GetRepo<Track, int>().GetAsync(spec) ?? throw new NotFoundException("This No Track Match This Id !!");
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
        var existingTrack = await unitOfWork.GetRepo<Track, int>().GetAsync(spec) ?? throw new NotFoundException("Track not found");
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

                    existingTrack.Sections ??= [];

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
                                    existingSection.Choices ??= [];
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
        if (id <= 0)
            throw new BadRequestException("invalid track id");

        var spec = new TrackByIdSpecification(id);
        var track = await unitOfWork.GetRepo<Track, int>().GetAsync(spec) ?? throw new NotFoundException("This No Track Match This Id !!");
        await unitOfWork.GetRepo<Track, int>().DeleteAsync(track);
        await unitOfWork.SaveChangesAsync();
        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse> DeleteSection(int id)
    {
        if (id <= 0)
            throw new BadRequestException("invalid section id");

        var section = new TrackSectionByIdSpecification(id);
        var existingSection = await unitOfWork
            .GetRepo<TrackSection, int>().GetAsync(section) ?? throw new NotFoundException("Section not found");
        await unitOfWork.GetRepo<TrackSection, int>().DeleteAsync(existingSection);
        await unitOfWork.SaveChangesAsync();

        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse> DeleteChoice(int id)
    {
        if (id <= 0)
            throw new BadRequestException("id is 0 or null");

        var choice = new TrackChoiceByIdSpecification(id);
        var existingChoice = await unitOfWork
            .GetRepo<TrackChoice, int>().GetAsync(choice) ?? throw new NotFoundException("Choice not found");
        await unitOfWork.GetRepo<TrackChoice, int>().DeleteAsync(existingChoice);
        await unitOfWork.SaveChangesAsync();

        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse<IEnumerable<AdminDTO>>> GetAllAdminsAsync(string role)
    {
        if (!string.IsNullOrEmpty(role)
            && (!string.Equals(role, StaticData.AdminRoleName, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(role, StaticData.SuperAdminRoleName, StringComparison.OrdinalIgnoreCase)))
            throw new BadRequestException("Invalid Role");

        var spec = new GetAdminsByRoleSpecification(role);

        var admins = await unitOfWork.GetRepo<ApplicationUser, int>().GetAllAsync(spec);

        if (admins == null || !admins.Any())
            throw new NotFoundException("No Admins Found");

        var adminsDto = mapper.Map<IEnumerable<AdminDTO>>(admins);

        return new APIResponse<IEnumerable<AdminDTO>>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = adminsDto
        };
    }

    public async Task<APIResponse> AddAdminAsync(AdminCreateDTO adminCreateDto)
    {
        if (adminCreateDto == null)
            throw new BadRequestException("invalid admin details");

        string role = adminCreateDto.Role;
        if (string.IsNullOrEmpty(role) ||
            (!string.Equals(role, StaticData.AdminRoleName, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(role, StaticData.SuperAdminRoleName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BadRequestException($"Role must be either '{StaticData.AdminRoleName}' or '{StaticData.SuperAdminRoleName}'");
        }

        role = string.Equals(role, StaticData.AdminRoleName, StringComparison.OrdinalIgnoreCase)
            ? StaticData.AdminRoleName
            : StaticData.SuperAdminRoleName;

        var admin = mapper.Map<ApplicationUser>(adminCreateDto);

        var createResult = await userManager.CreateAsync(admin, adminCreateDto.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            throw new BadRequestException($"Failed to create admin: {errors}");
        }

        var roleResult = await userManager.AddToRoleAsync(admin, role);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(admin);
            throw new InternalServerErrorException("Failed to assign role. User creation rolled back.");
        }

        return new APIResponse()
        {
            StatusCode = HttpStatusCode.Created,
        };
    }

    public async Task<APIResponse> UpdateAdminAsync(AdminUpdateDTO adminUpdateDto)
    {
        if (adminUpdateDto == null || string.IsNullOrEmpty(adminUpdateDto.Id))
            throw new BadRequestException("Invalid admin details or ID");

        string newRole = adminUpdateDto.Role!;
        if (!string.IsNullOrEmpty(newRole) &&
            !string.Equals(newRole, StaticData.AdminRoleName, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(newRole, StaticData.SuperAdminRoleName, StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("Invalid Role specified");
        }

        var user = await userManager.FindByIdAsync(adminUpdateDto.Id) ?? throw new NotFoundException("Admin not found");
        string currentEmail = user.Email!;

        mapper.Map(adminUpdateDto, user);

        if (!string.Equals(currentEmail, adminUpdateDto.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await userManager.SetEmailAsync(user, adminUpdateDto.Email);
            if (!emailResult.Succeeded)
            {
                var errors = string.Join(", ", emailResult.Errors.Select(e => e.Description));
                throw new BadRequestException($"Email Update Failed: {errors}");
            }

            var userResult = await userManager.SetUserNameAsync(user, adminUpdateDto.Email);
            if (!userResult.Succeeded)
            {
                var errors = string.Join(", ", userResult.Errors.Select(e => e.Description));
                throw new BadRequestException($"UserName Update Failed: {errors}");
            }
        }

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
            throw new BadRequestException($"Failed to update admin profile: {errors}");
        }

        if (!string.IsNullOrEmpty(newRole))
        {
            var currentRoles = await userManager.GetRolesAsync(user);

            if (!currentRoles.Contains(newRole, StringComparer.OrdinalIgnoreCase))
            {
                var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles);
                if (!removeResult.Succeeded)
                    throw new InternalServerErrorException("Failed to remove old roles");

                var addResult = await userManager.AddToRoleAsync(user, newRole);
                if (!addResult.Succeeded)
                    throw new InternalServerErrorException("Failed to add new role");
            }
        }

        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse> DeleteAdmin(string id)
    {
        if (string.IsNullOrEmpty(id))
            throw new BadRequestException("Invalid ID");

        var user = await userManager.FindByIdAsync(id) ?? throw new NotFoundException("Admin not found");
        var currentUserId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == id)
            throw new BadRequestException("You cannot delete your own account.");

        if (await userManager.IsInRoleAsync(user, StaticData.SuperAdminRoleName))
            throw new BadRequestException("Cannot delete a SuperAdmin.");

        var result = await userManager.DeleteAsync(user);

        if (!result.Succeeded)
            throw new InternalServerErrorException($"Failed to delete admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }
}
