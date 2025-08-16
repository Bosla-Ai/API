using System.Reflection;
using Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Persistence.Data.Configurations;

namespace Persistence.Data.Contexts;


public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> context) : base(context)
    {
        
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
    
    
    public DbSet<ApplicationUser> ApplicationUsers { get; set; }
    public DbSet<Customer> Customers { get; set; } // public DbSet<ResourceTag> ResourceTags { get; set; }
    public DbSet<DomainField> Domains { get; set; }
    public DbSet<Resource> Resources { get; set; }
    public DbSet<Roadmap> Roadmaps { get; set; }
    public DbSet<RoadmapResource> RoadmapResources { get; set; }
    public DbSet<Topic> Topics { get; set; }
    public DbSet<Track> Tracks { get; set; }
    public DbSet<TopicTechnology> TopicTechnologies { get; set; }
    public DbSet<TrackTechnology> TrackTechnologies { get; set; }
    public DbSet<LLMInteraction> LLMInteractions { get; set; }
}