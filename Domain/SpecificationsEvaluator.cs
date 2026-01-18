
using Domain.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Domain;

public static class SpecificationsEvaluator
{
    public static IQueryable<TEntity?> GetQuery<TEntity>(IQueryable<TEntity> query
        , Specifications<TEntity> specifications) where TEntity : class
    {
        if (specifications == null)
        {
            return query;
        }

        query = query.Where(specifications.Criteria);

        if (specifications.Includes.Any())
        {
            foreach (var include in specifications.Includes)
            {
                query = query.Include(include);
            }
        }

        if (specifications.IncludeStrings.Any())
        {
            foreach (var include in specifications.IncludeStrings)
            {
                query = query.Include(include);
            }
        }

        return query;
    }
}