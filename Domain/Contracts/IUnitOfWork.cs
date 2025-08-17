namespace Domain.Contracts;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync();

    IGenericRepository<TEntity, TKey> GetRepo<TEntity, TKey>() where TEntity : class;
}