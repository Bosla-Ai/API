using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public class ApplicationUserConfigurations : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.HasOne(a => a.CustomerProfile)
            .WithOne(c => c.ApplicationUser)
            .HasForeignKey<Customer>(a => a.ApplicationUserId);
        
        builder.HasMany(a => a.RoadMaps)
            .WithOne(r => r.ApplicationUser)
            .HasForeignKey(r => r.ApplicationUserId);
    }
}