using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public class ApplicationUserConfigurations : BaseEntityConfiguration<ApplicationUser>
{
    public override void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        ConfigureOneToOneRelationship(
            builder,
            a => a.CustomerProfile,
            c => c.ApplicationUser,
            (Customer c) => c.ApplicationUserId);
    }
}