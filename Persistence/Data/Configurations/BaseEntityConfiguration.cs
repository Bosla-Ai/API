using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Data.Configurations;

public abstract class BaseEntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity> 
    where TEntity : class
{
    public abstract void Configure(EntityTypeBuilder<TEntity> builder);

    protected void ConfigureOneToOneRelationship<TRelated>(
        EntityTypeBuilder<TEntity> builder,
        System.Linq.Expressions.Expression<System.Func<TEntity, TRelated?>> navigationExpression,
        System.Linq.Expressions.Expression<System.Func<TRelated, TEntity?>> inverseNavigationExpression,
        System.Linq.Expressions.Expression<System.Func<TRelated, object?>> foreignKeyExpression)
        where TRelated : class
    {
        builder.HasOne(navigationExpression)
            .WithOne(inverseNavigationExpression)
            .HasForeignKey(foreignKeyExpression);
    }
}