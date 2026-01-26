using System.Net;
using AutoMapper;
using Domain.Contracts;
using Domain.Entities;
using Domain.Exceptions;
using Domain.ModelsSpecifications.Administration.TrackSpecifications;
using Moq;
using Service.Implementations;
using Shared.DTOs.AdministrationDTOs.TrackDTOs;
using Xunit;

namespace BoslaAPI.Tests.Services;

public class AdministrationServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IGenericRepository<Track, int>> _mockTrackRepo;
    private readonly Mock<IGenericRepository<Domains, int>> _mockDomainsRepo;
    private readonly AdministrationService _administrationService;

    public AdministrationServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockMapper = new Mock<IMapper>();
        _mockTrackRepo = new Mock<IGenericRepository<Track, int>>();
        _mockDomainsRepo = new Mock<IGenericRepository<Domains, int>>();

        _mockUnitOfWork.Setup(u => u.GetRepo<Track, int>()).Returns(_mockTrackRepo.Object);
        _mockUnitOfWork.Setup(u => u.GetRepo<Domains, int>()).Returns(_mockDomainsRepo.Object);

        _administrationService = new AdministrationService(_mockUnitOfWork.Object, _mockMapper.Object);
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
    public async Task UpdateFullTrack_ShouldUpdateTrack_WhenTrackExists()
    {
        // Arrange
        var updateDto = new TrackUpdateFullDTO { Id = 1, Title = "Updated Track" };
        var existingTrack = new Track { Id = 1, Title = "Old Track" };

        _mockTrackRepo.Setup(r => r.GetAsync(It.IsAny<TrackWithFullStructureSpecification>()))
            .ReturnsAsync(existingTrack);

        // Mock Mapper: Map(source, dest) -> void
        _mockMapper.Setup(m => m.Map(updateDto, existingTrack));

        _mockTrackRepo.Setup(r => r.UpdateAsync(existingTrack)).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _administrationService.UpdateFullTrack(updateDto);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        _mockTrackRepo.Verify(r => r.UpdateAsync(existingTrack), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteTrack_ShouldDeleteTrack_WhenTrackExists()
    {
        // Arrange
        var trackId = 1;
        var trackEntity = new Track { Id = trackId };

        _mockTrackRepo.Setup(r => r.GetAsync(It.IsAny<TrackByIdSpecification>()))
            .ReturnsAsync(trackEntity);
        _mockTrackRepo.Setup(r => r.DeleteAsync(trackEntity)).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _administrationService.DeleteTrack(trackId);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        _mockTrackRepo.Verify(r => r.DeleteAsync(trackEntity), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }
}
