using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public class CustomerConfigurations : BaseEntityConfiguration<Customer>
{
    public override void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(pk => pk.ApplicationUserId);

        builder.Property(c => c.UserLevel)
            .HasConversion<string>();
    }
}