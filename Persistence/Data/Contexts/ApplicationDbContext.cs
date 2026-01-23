using System.Reflection;
using Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Persistence.Data.Configurations;
using Shared.DTOs.DashboardDTOs;

namespace Persistence.Data.Contexts;


public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> context) : base(context)
    {

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Configure keyless entity for stored procedure result - no backing table
        modelBuilder.Entity<DashboardFlatResult>()
            .HasNoKey()
            .ToView(null);

        base.OnModelCreating(modelBuilder);
    }


    public DbSet<ApplicationUser> ApplicationUsers { get; set; }
    public DbSet<Customer> Customers { get; set; } // public DbSet<ResourceTag> ResourceTags { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Course> Courses { get; set; }

    public async Task<List<DashboardFlatResult>> GetDomainsHierarchyAsync(bool? isActive = null)
    {
        var param = new SqlParameter("@IsActive", isActive.HasValue ? isActive.Value : DBNull.Value);
        return await Set<DashboardFlatResult>()
            .FromSqlRaw("EXEC sp_GetAllDomainsWithHierarchy @IsActive", param)
            .ToListAsync();
    }
}