using System.Linq.Expressions;

namespace Domain.Contracts;

public abstract class Specifications<TEntity>
{
    public Expression<Func<TEntity, bool>> Criteria { get; }
    public List<Expression<Func<TEntity, object>>> Includes { get; } 
    
    public Specifications(Expression<Func<TEntity, bool>> criteria)
    {
        Criteria = criteria;
        Includes = new  List<Expression<Func<TEntity, object>>>();
    }

    public void AddInclude(Expression<Func<TEntity, object>> includeExpression)
    {
        Includes.Add(includeExpression);
    }
}