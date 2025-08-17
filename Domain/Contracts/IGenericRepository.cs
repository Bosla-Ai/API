using System.Linq.Expressions;

namespace Domain.Contracts;

public interface IGenericRepository<TEntity,Tkey> 
{
    public Task<TEntity> GetIdAsync(Tkey id);
    public Task<TEntity> GetAsync(Specifications<TEntity> specification = null);
    public Task<IEnumerable<TEntity>> GetAllAsync(Specifications<TEntity> specification = null);
    public Task CreateAsync(TEntity entity);
    public Task UpdateAsync(TEntity entity);
    public Task DeleteAsync(TEntity entity);
    
}