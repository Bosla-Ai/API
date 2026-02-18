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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Moq;
using Service.Implementations;
using Shared;
using Shared.DTOs.AdministrationDTOs.AdminDTOs;
using Shared.DTOs.AdministrationDTOs.DomainDTOs;
using Shared.DTOs.AdministrationDTOs.TrackChoiceDTOs;
using Shared.DTOs.AdministrationDTOs.TrackDTOs;
using Shared.DTOs.AdministrationDTOs.TrackSectionDTOs;

namespace BoslaAPI.Tests.Services;

public class AdministrationServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IGenericRepository<Track, int>> _mockTrackRepo;
    private readonly Mock<IGenericRepository<Domains, int>> _mockDomainsRepo;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly AdministrationService _administrationService;

    public AdministrationServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockMapper = new Mock<IMapper>();
        _mockTrackRepo = new Mock<IGenericRepository<Track, int>>();
        _mockDomainsRepo = new Mock<IGenericRepository<Domains, int>>();

        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(userStore.Object, null, null, null, null, null, null, null, null);
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        _mockUnitOfWork.Setup(u => u.GetRepo<Track, int>()).Returns(_mockTrackRepo.Object);
        _mockUnitOfWork.Setup(u => u.GetRepo<Domains, int>()).Returns(_mockDomainsRepo.Object);

        _administrationService = new AdministrationService(_mockUnitOfWork.Object, _mockMapper.Object, _mockUserManager.Object, _mockHttpContextAccessor.Object);
    }

    [Fact]
    public async Task AddTrackFull_ShouldCreateTrack_WhenDtoIsValid()
    {
        // Arrange
        var createDto = new TrackCreateFullDTO { Title = "New Track", DomainId = 1 };
        var trackEntity = new Track { Id = 1, Title = "New Track" };

        _mockMapper.Setup(m => m.Map<Track>(createDto)).Returns(trackEntity);
        _mockTrackRepo.Setup(r => r.CreateAsync(trackEntity)).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _administrationService.AddTrackFull(createDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(1, result.Data);
        _mockTrackRepo.Verify(r => r.CreateAsync(trackEntity), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetFullTrack_ShouldReturnTrack_WhenIdExists()
    {
        // Arrange
        var trackId = 1;
        var trackEntity = new Track { Id = trackId, Title = "Full Stack" };
        var trackDto = new TrackFullDTO { Id = trackId, Title = "Full Stack" };

        _mockTrackRepo.Setup(r => r.GetAsync(It.IsAny<TrackWithFullStructureSpecification>()))
            .ReturnsAsync(trackEntity);
        _mockMapper.Setup(m => m.Map<TrackFullDTO>(trackEntity)).Returns(trackDto);

        // Act
        var result = await _administrationService.GetFullTrack(trackId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(trackDto, result.Data);
    }

    [Fact]
    public async Task GetFullTrack_ShouldThrowNotFound_WhenTrackDoesNotExist()
    {
        // Arrange
        var trackId = 99;
        _mockTrackRepo.Setup(r => r.GetAsync(It.IsAny<TrackWithFullStructureSpecification>()))
            .ReturnsAsync((Track?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _administrationService.GetFullTrack(trackId));
    }

    [Fact]
    public async Task UpdateFullTrack_ShouldHandleCreateUpdateAndIgnore_Correctly()
    {
        // Arrange
        var trackId = 1;
        var existingTrack = new Track
        {
            Id = trackId,
            Sections =
            [
                new TrackSection
                {
                    Id = 10,
                    Title = "Old Section",
                    Choices =
                    [
                        new TrackChoice { Id = 100, Label = "Old Choice" }
                    ]
                }
            ]
        };

        var updateDto = new TrackUpdateFullDTO
        {
            Id = trackId,
            Title = "Updated Track",
            Sections =
            [
                // 1. Create (Id = 0)
                new TrackSectionUpdateFullDTO
                {
                    Id = 0,
                    Title = "New Section",
                    Choices =
                    [
                        new TrackChoiceUpdateDTO { Id = 0, Title = "New Choice" }
                    ]
                },
                // 2. Update (Id = 10)
                new TrackSectionUpdateFullDTO
                {
                    Id = 10,
                    Title = "Updated Section",
                    Choices =
                    [
                        new TrackChoiceUpdateDTO { Id = 100, Title = "Updated Choice" }
                    ]
                },
                // 3. Ignore (Id = null)
                new TrackSectionUpdateFullDTO
                {
                    Id = null,
                    Title = "Ignored Section"
                }
            ]
        };

        _mockTrackRepo.Setup(r => r.GetAsync(It.IsAny<TrackWithFullStructureSpecification>()))
            .ReturnsAsync(existingTrack);

        // Mock Root Map
        _mockMapper.Setup(m => m.Map(updateDto, existingTrack))
            .Callback<TrackUpdateFullDTO, Track>((src, dest) => dest.Title = src.Title);

        // Mock Create Section Map
        _mockMapper.Setup(m => m.Map<TrackSection>(It.Is<TrackSectionUpdateFullDTO>(s => s.Id == 0)))
            .Returns((TrackSectionUpdateFullDTO s) => new TrackSection { Title = s.Title, Choices = [] });

        // Mock Create Choice Map (Nested in New Section)
        _mockMapper.Setup(m => m.Map<ICollection<TrackChoice>>(It.IsAny<ICollection<TrackChoiceUpdateDTO>>()))
            .Returns((ICollection<TrackChoiceUpdateDTO> source) => [.. source.Select(c => new TrackChoice { Label = c.Title })]);

        // Mock Update Section Map
        _mockMapper.Setup(m => m.Map(It.Is<TrackSectionUpdateFullDTO>(s => s.Id == 10), It.IsAny<TrackSection>()))
            .Callback<TrackSectionUpdateFullDTO, TrackSection>((src, dest) => dest.Title = src.Title);

        // Mock Update Choice Map
        _mockMapper.Setup(m => m.Map(It.Is<TrackChoiceUpdateDTO>(c => c.Id == 100), It.IsAny<TrackChoice>()))
            .Callback<TrackChoiceUpdateDTO, TrackChoice>((src, dest) => dest.Label = src.Title);

        _mockTrackRepo.Setup(r => r.UpdateAsync(existingTrack)).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _administrationService.UpdateFullTrack(updateDto);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        // Verify Root Update
        Assert.Equal("Updated Track", existingTrack.Title);

        // Verify Sections
        // Existing section (Id 10) should be updated
        var updatedSection = existingTrack.Sections.First(s => s.Id == 10);
        Assert.Equal("Updated Section", updatedSection.Title);
        Assert.Equal("Updated Choice", updatedSection.Choices.First(c => c.Id == 100).Label);

        // New section should be added (Count should be 2: Old + New. Ignored is ignored)
        Assert.Equal(2, existingTrack.Sections.Count);
        var newSection = existingTrack.Sections.FirstOrDefault(s => s.Title == "New Section");
        Assert.NotNull(newSection);
        // Verify nested choice in new section
        Assert.Single(newSection.Choices);
        Assert.Equal("New Choice", newSection.Choices.First().Label);

        _mockTrackRepo.Verify(r => r.UpdateAsync(existingTrack), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetDomainsAsync_ShouldReturnDomains_WhenExists()
    {
        // Arrange
        var domainList = new List<Domains> { new() { Id = 1, Title = "Tech" } };
        var domainDtoList = new List<DomainsDTO> { new() { Id = 1, Title = "Tech" } };

        _mockDomainsRepo.Setup(r => r.GetAllAsync(It.IsAny<DomainsIsActiveSpecifications>()))
            .ReturnsAsync(domainList);
        _mockMapper.Setup(m => m.Map<IEnumerable<DomainsDTO>>(domainList))
            .Returns(domainDtoList);

        // Act
        var result = await _administrationService.GetDomainsAsync(true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(domainDtoList, result.Data);
    }

    [Fact]
    public async Task GetDomainsAsync_ShouldThrowNotFound_WhenNoDomainsExist()
    {
        // Arrange
        _mockDomainsRepo.Setup(r => r.GetAllAsync(It.IsAny<DomainsIsActiveSpecifications>()))
            .ReturnsAsync((IEnumerable<Domains>?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _administrationService.GetDomainsAsync(true));
    }

    [Fact]
    public async Task GetDomainAsync_ShouldReturnDomain_WhenIdExists()
    {
        // Arrange
        var domainId = 1;
        var domain = new Domains { Id = domainId, Title = "Tech" };
        var domainDto = new DomainsDTO { Id = domainId, Title = "Tech" };

        _mockDomainsRepo.Setup(r => r.GetAsync(It.IsAny<DomainByIdSpecifications>()))
            .ReturnsAsync(domain);
        _mockMapper.Setup(m => m.Map<DomainsDTO>(domain))
            .Returns(domainDto);

        // Act
        var result = await _administrationService.GetDomainAsync(domainId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(domainDto, result.Data);
    }

    [Fact]
    public async Task GetDomainAsync_ShouldThrowBadRequest_WhenIdInvalid()
    {
        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => _administrationService.GetDomainAsync(0));
    }

    [Fact]
    public async Task GetDomainAsync_ShouldThrowNotFound_WhenDomainDoesNotExist()
    {
        // Arrange
        var domainId = 1;
        _mockDomainsRepo.Setup(r => r.GetAsync(It.IsAny<DomainByIdSpecifications>()))
            .ReturnsAsync((Domains?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _administrationService.GetDomainAsync(domainId));
    }

    [Fact]
    public async Task AddDomain_ShouldCreateDomain_WhenDtoIsValid()
    {
        // Arrange
        var createDto = new DomainCreateDTO { Title = "New Domain" };
        var domain = new Domains { Id = 1, Title = "New Domain" };

        _mockMapper.Setup(m => m.Map<Domains>(createDto)).Returns(domain);
        _mockDomainsRepo.Setup(r => r.CreateAsync(domain)).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _administrationService.AddDomain(createDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(1, result.Data);
        _mockDomainsRepo.Verify(r => r.CreateAsync(domain), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateDomain_ShouldUpdateDomain_WhenDtoIsValid()
    {
        // Arrange
        var updateDto = new DomainUpdateDTO { Id = 1, Title = "Updated Domain" };
        var domain = new Domains { Id = 1, Title = "Updated Domain" };

        _mockMapper.Setup(m => m.Map<Domains>(updateDto)).Returns(domain);
        _mockDomainsRepo.Setup(r => r.UpdateAsync(domain)).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _administrationService.UpdateDomain(updateDto);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        _mockDomainsRepo.Verify(r => r.UpdateAsync(domain), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteDomain_ShouldDeleteDomain_WhenIdExists()
    {
        // Arrange
        var domainId = 1;
        var domain = new Domains { Id = domainId, Title = "Tech" };

        _mockDomainsRepo.Setup(r => r.GetAsync(It.IsAny<DomainByIdSpecifications>()))
            .ReturnsAsync(domain);
        _mockDomainsRepo.Setup(r => r.DeleteAsync(domain)).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _administrationService.DeleteDomain(domainId);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        _mockDomainsRepo.Verify(r => r.DeleteAsync(domain), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetTracks_ShouldReturnTracks_WhenDomainIdExists()
    {
        // Arrange
        var domainId = 1;
        var trackList = new List<Track> { new() { Id = 1, Title = "Track 1", DomainId = domainId } };
        var trackDtoList = new List<TrackDTO> { new() { Id = 1, Title = "Track 1" } };

        _mockTrackRepo.Setup(r => r.GetAllAsync(It.IsAny<TracksByDomainIdSpecification>()))
            .ReturnsAsync(trackList);
        _mockMapper.Setup(m => m.Map<IEnumerable<TrackDTO>>(trackList))
            .Returns(trackDtoList);

        // Act
        var result = await _administrationService.GetTracks(domainId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(trackDtoList, result.Data);
    }

    [Fact]
    public async Task DeleteTrack_ShouldDeleteTrack_WhenIdExists()
    {
        // Arrange
        var trackId = 1;
        var track = new Track { Id = trackId, Title = "Track 1" };

        _mockTrackRepo.Setup(r => r.GetAsync(It.IsAny<TrackByIdSpecification>()))
            .ReturnsAsync(track);
        _mockTrackRepo.Setup(r => r.DeleteAsync(track)).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _administrationService.DeleteTrack(trackId);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        _mockTrackRepo.Verify(r => r.DeleteAsync(track), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteSection_ShouldDeleteSection_WhenIdExists()
    {
        // Arrange
        var sectionId = 1;
        var section = new TrackSection { Id = sectionId, Title = "Section 1" };
        var sectionRepo = new Mock<IGenericRepository<TrackSection, int>>();

        _mockUnitOfWork.Setup(u => u.GetRepo<TrackSection, int>()).Returns(sectionRepo.Object);
        sectionRepo.Setup(r => r.GetAsync(It.IsAny<TrackSectionByIdSpecification>()))
            .ReturnsAsync(section);
        sectionRepo.Setup(r => r.DeleteAsync(section)).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _administrationService.DeleteSection(sectionId);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        sectionRepo.Verify(r => r.DeleteAsync(section), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteChoice_ShouldDeleteChoice_WhenIdExists()
    {
        // Arrange
        var choiceId = 1;
        var choice = new TrackChoice { Id = choiceId, Label = "Choice 1" };
        var choiceRepo = new Mock<IGenericRepository<TrackChoice, int>>();

        _mockUnitOfWork.Setup(u => u.GetRepo<TrackChoice, int>()).Returns(choiceRepo.Object);
        choiceRepo.Setup(r => r.GetAsync(It.IsAny<TrackChoiceByIdSpecification>()))
            .ReturnsAsync(choice);
        choiceRepo.Setup(r => r.DeleteAsync(choice)).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _administrationService.DeleteChoice(choiceId);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        choiceRepo.Verify(r => r.DeleteAsync(choice), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetAllAdminsAsync_ShouldReturnAdmins_WhenRoleIsValid()
    {
        // Arrange
        var role = StaticData.AdminRoleName;
        var admins = new List<ApplicationUser> { new() { Id = "1", UserName = "admin" } };
        var adminDtos = new List<AdminDTO> { new() { Id = "1", UserName = "admin" } };

        var userRepo = new Mock<IGenericRepository<ApplicationUser, int>>();
        _mockUnitOfWork.Setup(u => u.GetRepo<ApplicationUser, int>()).Returns(userRepo.Object);
        userRepo.Setup(r => r.GetAllAsync(It.IsAny<GetAdminsByRoleSpecification>()))
            .ReturnsAsync(admins);
        _mockMapper.Setup(m => m.Map<IEnumerable<AdminDTO>>(admins))
            .Returns(adminDtos);

        // Act
        var result = await _administrationService.GetAllAdminsAsync(role);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(adminDtos, result.Data);
    }

    [Fact]
    public async Task AddAdminAsync_ShouldCreateAdmin_WhenDtoIsValid()
    {
        // Arrange
        var createDto = new AdminCreateDTO { UserName = "admin", Password = "Password123!", Role = StaticData.AdminRoleName };
        var user = new ApplicationUser { UserName = "admin" };

        _mockMapper.Setup(m => m.Map<ApplicationUser>(createDto)).Returns(user);
        _mockUserManager.Setup(u => u.CreateAsync(user, createDto.Password))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(u => u.AddToRoleAsync(user, StaticData.AdminRoleName))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _administrationService.AddAdminAsync(createDto);

        // Assert
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        _mockUserManager.Verify(u => u.CreateAsync(user, createDto.Password), Times.Once);
        _mockUserManager.Verify(u => u.AddToRoleAsync(user, StaticData.AdminRoleName), Times.Once);
    }

    [Fact]
    public async Task UpdateAdminAsync_ShouldUpdateAdmin_WhenDtoIsValid()
    {
        // Arrange
        var adminId = "1";
        var updateDto = new AdminUpdateDTO { Id = adminId, Email = "new@example.com", Role = StaticData.AdminRoleName };
        var user = new ApplicationUser { Id = "1", Email = "old@example.com", UserName = "old@example.com" };

        _mockUserManager.Setup(u => u.FindByIdAsync(adminId)).ReturnsAsync(user);
        _mockUserManager.Setup(u => u.SetEmailAsync(user, updateDto.Email)).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(u => u.SetUserNameAsync(user, updateDto.Email)).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(u => u.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(u => u.GetRolesAsync(user)).ReturnsAsync([StaticData.AdminRoleName]);

        // Act
        var result = await _administrationService.UpdateAdminAsync(updateDto);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        _mockUserManager.Verify(u => u.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task DeleteAdmin_ShouldDeleteAdmin_WhenIdIsValid()
    {
        // Arrange
        var adminId = "2";
        var user = new ApplicationUser { Id = "2", UserName = "tobedeleted" };

        // Mock HttpContext for current user check
        var context = new DefaultHttpContext();
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "1") }; // Current user is 1
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        context.User = claimsPrincipal;

        _mockHttpContextAccessor.Setup(h => h.HttpContext).Returns(context);
        _mockUserManager.Setup(u => u.FindByIdAsync(adminId)).ReturnsAsync(user);
        _mockUserManager.Setup(u => u.IsInRoleAsync(user, StaticData.SuperAdminRoleName)).ReturnsAsync(false);
        _mockUserManager.Setup(u => u.DeleteAsync(user)).ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _administrationService.DeleteAdmin(adminId);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        _mockUserManager.Verify(u => u.DeleteAsync(user), Times.Once);
    }
}
