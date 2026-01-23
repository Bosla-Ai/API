using Shared.DTOs.DashboardDTOs;

namespace Domain.Contracts;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync();

    IGenericRepository<TEntity, TKey> GetRepo<TEntity, TKey>() where TEntity : class;

    Task<List<DashboardFlatResult>> GetDomainsHierarchyAsync(bool? isActive = null);
}