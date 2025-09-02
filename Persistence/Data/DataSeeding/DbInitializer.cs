using Domain.Contracts;
using Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Persistence.Data.Contexts;
using Persistence.Seeder;

namespace Persistence.Data.DataSeeding;

public class DbInitializer(ApplicationDbContext dbContext,RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager)
    : IDbInitializer
{
    public async Task InitializeDbAsync()
    {
        throw new NotImplementedException();
    }

    public async Task InitializeRolesAsync()
    {
        if (!roleManager.Roles.Any())
        {
            await RoleSeeder.SeedRoles(roleManager);
        }
    }
}