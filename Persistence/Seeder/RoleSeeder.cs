using Microsoft.AspNetCore.Identity;
using Shared;

namespace Persistence.Seeder;

public static class RoleSeeder
{
    public static async Task SeedRoles(RoleManager<IdentityRole> roleManager)
    {
        string[] roleNames = { StaticData.SuperAdminRoleName, StaticData.AdminRoleName, StaticData.CustomerRoleName };
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
    }
}