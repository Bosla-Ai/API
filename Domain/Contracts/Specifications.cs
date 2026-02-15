using System.Linq.Expressions;

namespace Domain.Contracts;

public abstract class Specifications<TEntity>(Expression<Func<TEntity, bool>> criteria)
{
    public Expression<Func<TEntity, bool>> Criteria { get; } = criteria;
    public List<Expression<Func<TEntity, object>>> Includes { get; } = [];

    public List<string> IncludeStrings { get; } = [];

    public void AddInclude(Expression<Func<TEntity, object>> includeExpression)
    {
        Includes.Add(includeExpression);
    }

    public void AddInclude(string includeString)
    {
        IncludeStrings.Add(includeString);
    }
}