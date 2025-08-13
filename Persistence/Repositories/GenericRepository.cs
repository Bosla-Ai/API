using Domain.Contracts;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Persistence.Repositories;

public class GenericRepository<TEntity,TKey> : IGenericRepository<TEntity,TKey>
{
    private readonly IUnitOfWork _unitOfWork;

    public GenericRepository(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    public async Task<TEntity> GetIdAsync(TKey id)
    {
        return await _unitOfWork.GetRepo<TEntity, TKey>().GetIdAsync(id);
    }

    public async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        return await _unitOfWork.GetRepo<TEntity, TKey>().GetAllAsync();
    }

    public async Task CreateAsync(TEntity entity)
    {
        await _unitOfWork.GetRepo<TEntity, TKey>().CreateAsync(entity);
    }

    public async Task UpdateAsync(TEntity entity)
    {
        await _unitOfWork.GetRepo<TEntity, TKey>().UpdateAsync(entity);
    }

    public async Task DeleteAsync(TEntity entity)
    {
        await _unitOfWork.GetRepo<TEntity, TKey>().DeleteAsync(entity);
    }
}