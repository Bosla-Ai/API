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
        builder.Property(a => a.Role)
            .HasMaxLength(50)
            .HasDefaultValue("CustomerRole");
        builder.HasCheckConstraint("CK_ApplicationUser_Role",
            "[Role] IN ('CustomerRole','AdminRole','SuperAdminRole')");
    }
}