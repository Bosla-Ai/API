using System.Net;
using AutoMapper;
using Domain.Contracts;
using Domain.Entities;
using Domain.Exceptions;
using Domain.ModelsSpecifications.Administration.TrackSpecifications;
using Moq;
using Service.Implementations;
using Shared.DTOs.AdministrationDTOs.TrackDTOs;
using Shared.DTOs.AdministrationDTOs.TrackSectionDTOs;
using Shared.DTOs.AdministrationDTOs.TrackChoiceDTOs;
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
}
