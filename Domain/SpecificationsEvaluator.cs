
using Domain.Contracts;

namespace Domain;

public static class SpecificationsEvaluator
{
    public static IQueryable<TEntity?> GetQuery<TEntity>(IQueryable<TEntity> query 
        ,Specifications<TEntity> specifications) where TEntity : class
    {
        if (specifications == null)
        {
            return query;
        }

        query = query.Where(specifications.Criteria);
        return query;
    }
}