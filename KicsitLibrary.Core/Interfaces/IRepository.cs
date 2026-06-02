using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using KicsitLibrary.Core.Entities;

namespace KicsitLibrary.Core.Interfaces
{
    public interface IRepository<T> where T : EntityBase
    {
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
        Task SoftDeleteAsync(T entity, string reason, int? userId);
        Task SaveChangesAsync();
    }
}
