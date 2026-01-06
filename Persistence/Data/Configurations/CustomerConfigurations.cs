using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace Persistence.Data.Configurations;

public class CustomerConfigurations : IEntityTypeConfiguration<Customer> // app user and customer
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(pk => pk.ApplicationUserId);

        builder.HasOne(p => p.ApplicationUser)
            .WithOne(c => c.CustomerProfile)
            .HasForeignKey<Customer>(c => c.ApplicationUserId);

        builder.Property(c => c.UserLevel)
            .HasConversion<string>();
    }
}