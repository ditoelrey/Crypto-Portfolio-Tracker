using System.Linq.Expressions;

namespace CryptoApp.Domain.Repositories;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync<TId>(TId id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
    Task SaveChangesAsync();
}
