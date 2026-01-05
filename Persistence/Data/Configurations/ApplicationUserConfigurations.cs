using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public class ApplicationUserConfigurations : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // Properties
        builder.Property(a => a.Role)
            .HasMaxLength(50)
            .HasDefaultValue("CustomerRole");

        // Constraints
        builder.HasCheckConstraint("CK_ApplicationUser_Role",
            "[Role] IN ('CustomerRole','AdminRole','SuperAdminRole')");

        // Relationships
        builder.HasOne(a => a.CustomerProfile)
            .WithOne(c => c.ApplicationUser)
            .HasForeignKey<Customer>(a => a.ApplicationUserId);

    }
}