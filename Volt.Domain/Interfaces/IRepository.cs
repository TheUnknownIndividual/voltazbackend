using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Domain.Interfaces
{
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(object id, CancellationToken ct = default);

        Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

        Task<T?> FirstOrDefaultNoTrackingAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

        Task<List<T>> ListNoTrackingAsync(CancellationToken ct = default);

        Task<List<T>> ListNoTrackingAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

        Task AddAsync(T entity, CancellationToken ct = default);

        void Update(T entity);

        void Remove(T entity);

    }
}
