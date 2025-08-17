using System.Collections.Concurrent;
using Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Persistence.Data.Contexts;

namespace Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    public ConcurrentDictionary<string, object> Repositories;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
        Repositories = new ConcurrentDictionary<string, object>();
    }
    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public IGenericRepository<TEntity, TKey> GetRepo<TEntity, TKey>() where TEntity : class
    {
         return (IGenericRepository<TEntity,TKey>) Repositories.GetOrAdd(typeof(TEntity).Name,(name)=>new GenericRepository<TEntity,TKey>(_context));
    }
}