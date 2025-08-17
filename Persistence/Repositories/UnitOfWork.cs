using System.Collections.Concurrent;
using Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Persistence.Data.Contexts;

namespace Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    public ConcurrentDictionary<string, object> _repositories;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
        _repositories = new ConcurrentDictionary<string, object>();
    }
    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public IGenericRepository<TEntity, TKey> GetRepo<TEntity, TKey>() where TEntity : class
    {
        var typeName = typeof(TEntity).Name;

        return (IGenericRepository<TEntity, TKey>) _repositories.GetOrAdd(typeName,
            _ => new GenericRepository<TEntity, TKey>(_context));
    }
}