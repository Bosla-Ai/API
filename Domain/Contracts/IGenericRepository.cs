namespace Domain.Contracts;

public interface IGenericRepository<TEntity,Tkey> 
{
    public Task<TEntity> GetIdAsync(Tkey id);
    public Task<IEnumerable<TEntity>> GetAllAsync();
    public Task CreateAsync(TEntity entity);
    public Task UpdateAsync(TEntity entity);
    public Task DeleteAsync(TEntity entity);
    
}