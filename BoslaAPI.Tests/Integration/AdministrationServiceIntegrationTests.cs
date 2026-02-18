using AutoMapper;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Persistence.Data.Contexts;
using Persistence.Repositories;
using Service.Implementations;
using Service.MappingProfiles;
using Shared.DTOs.AdministrationDTOs.TrackChoiceDTOs;
using Shared.DTOs.AdministrationDTOs.TrackDTOs;
using Shared.DTOs.AdministrationDTOs.TrackSectionDTOs;

namespace BoslaAPI.Tests.Integration;

public class AdministrationServiceIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UnitOfWork _unitOfWork;
    private readonly AdministrationService _service;
    private readonly IMapper _mapper;

    public AdministrationServiceIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _unitOfWork = new UnitOfWork(_dbContext);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => { }, typeof(AdministrationMapping).Assembly);
        var provider = services.BuildServiceProvider();
        _mapper = provider.GetRequiredService<IMapper>();

        _service = new AdministrationService(_unitOfWork, _mapper, new Mock<UserManager<ApplicationUser>>(new Mock<IUserStore<ApplicationUser>>().Object, null, null, null, null, null, null, null, null).Object, new Mock<IHttpContextAccessor>().Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task UpdateFullTrack_UpdatesGraphCorrectly_WithAdditionsAndModifications()
    {
        // Arrange - Seed Initial Graph
        var track = new Track
        {
            Id = 1,
            Title = "Original Track",
            Description = "Original Desc",
            Sections =
            [
                new TrackSection
                {
                    Id = 10,
                    Title = "Original Section",
                    Choices =
                    [
                        new TrackChoice { Id = 100, Label = "Original Choice", SectionId = 10 }
                    ]
                }
            ]
        };
        _dbContext.Set<Track>().Add(track);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear(); // Detach to simulate fresh request

        var updateDto = new TrackUpdateFullDTO
        {
            Id = 1,
            Title = "Updated Track",
            Description = "Updated Desc",
            IconUrl = "http://example.com/icon.png",
            FixedTagsPayload = "tag1",
            Sections =
            [
                // Modification: Update existing Section 10
                new TrackSectionUpdateFullDTO
                {
                    Id = 10,
                    TrackId = 1,
                    Title = "Updated Section",
                    OrderIndex = 1,
                    Choices =
                    [
                        // Modification: Update existing Choice 100
                        new TrackChoiceUpdateDTO
                        {
                            Id = 100,
                            SectionId = 10,
                            Title = "Updated Choice",
                            IsDefault = true
                        },
                        // Addition: Add New Choice to existing Section
                        new TrackChoiceUpdateDTO
                        {
                            Id = 0, // 0 = New
                            SectionId = 10,
                            Title = "New Choice In Old Section",
                            IsDefault = false
                        }
                    ]
                },
                // Addition: Add New Section
                new TrackSectionUpdateFullDTO
                {
                    Id = 0,
                    TrackId = 1,
                    Title = "New Section",
                    OrderIndex = 2,
                    Choices =
                    [
                        new TrackChoiceUpdateDTO
                        {
                            Id = 0,
                            Title = "New Choice In New Section",
                            IsDefault = true
                        }
                    ]
                }
            ]
        };

        // Act
        await _service.UpdateFullTrack(updateDto);

        // Assert
        var updatedTrack = await _dbContext.Set<Track>()
            .Include(t => t.Sections!)
            .ThenInclude(s => s.Choices)
            .FirstOrDefaultAsync(t => t.Id == 1);

        Assert.NotNull(updatedTrack);
        Assert.Equal("Updated Track", updatedTrack.Title);
        Assert.Equal(2, updatedTrack.Sections.Count);

        // Verify Modified Section
        var section10 = updatedTrack.Sections.FirstOrDefault(s => s.Id == 10);
        Assert.NotNull(section10);
        Assert.Equal("Updated Section", section10.Title);
        Assert.Equal(2, section10.Choices.Count);

        var choice100 = section10.Choices.FirstOrDefault(c => c.Id == 100);
        Assert.NotNull(choice100);
        Assert.Equal("Updated Choice", choice100.Label);

        var newChoiceInOldSection = section10.Choices.FirstOrDefault(c => c.Id != 100);
        Assert.NotNull(newChoiceInOldSection);
        Assert.Equal("New Choice In Old Section", newChoiceInOldSection.Label);

        // Verify New Section
        var newSection = updatedTrack.Sections.FirstOrDefault(s => s.Id != 10);
        Assert.NotNull(newSection);
        Assert.Equal("New Section", newSection.Title);
        Assert.Single(newSection.Choices); // Assertion fix
        Assert.Equal("New Choice In New Section", newSection.Choices.First().Label);
    }
}
