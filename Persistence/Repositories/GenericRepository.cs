using Domain;
using Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Persistence.Data.Contexts;

namespace Persistence.Repositories;

public class GenericRepository<TEntity, TKey> : IGenericRepository<TEntity, TKey> where TEntity : class
{
    private readonly ApplicationDbContext _context;
    private readonly DbSet<TEntity> _dbSet;
    public GenericRepository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<TEntity>();
    }
    public async Task<TEntity> GetIdAsync(TKey id)
    {
        return await _dbSet.FindAsync(id);
    }

    public async Task<TEntity?> GetAsync(Specifications<TEntity> specification = null)
    {
        IQueryable<TEntity> query = _dbSet;
        if (specification != null)
        {
            query = SpecificationsEvaluator.GetQuery(query, specification)!;
        }
        return await query.FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TEntity>> GetAllAsync(Specifications<TEntity> specification = null)
    {
        IQueryable<TEntity> query = _dbSet;
        if (specification != null)
        {
            query = SpecificationsEvaluator.GetQuery(query, specification)!;
        }
        return await query.ToListAsync();
    }

    public async Task CreateAsync(TEntity entity)
    {
        await _dbSet.AddAsync(entity);
    }

    public Task UpdateAsync(TEntity entity)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(TEntity entity)
    {
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }
    public async Task<int> CountAsync(Specifications<TEntity> specification = null)
    {
        IQueryable<TEntity> query = _dbSet;
        if (specification is not null)
        {
            query = SpecificationsEvaluator.GetQuery(query, specification)!;
        }
        return await query.CountAsync();
    }

    public async Task<int> CountDistinctAsync<TProperty>(
        Specifications<TEntity> specification
        , Expression<Func<TEntity, TProperty>> selector)
    {
        IQueryable<TEntity> query = _dbSet;
        if (specification != null)
        {
            query = SpecificationsEvaluator.GetQuery(query, specification)!;
        }

        return await query.Select(selector).Distinct().CountAsync();
    }
}