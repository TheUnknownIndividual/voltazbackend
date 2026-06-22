using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Interfaces;
using Volt.Infrastructure.Data;

namespace Volt.Infrastructure.Repositories
{
    public sealed class GenericRepository<T> : IRepository<T> where T : class
    {
        private readonly DataContext _context;
        private readonly DbSet<T> _dbSet;

        public GenericRepository(DataContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }
        public Task AddAsync(T entity, CancellationToken ct = default) => _dbSet.AddAsync(entity, ct).AsTask();

        public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => _dbSet.AnyAsync(predicate, ct);

        public Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => _dbSet.FirstOrDefaultAsync(predicate, ct);

        public Task<T> FirstOrDefaultNoTrackingAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => _dbSet.AsNoTracking().FirstOrDefaultAsync(predicate, ct);

        public Task<T> GetByIdAsync(object id, CancellationToken ct = default) => _dbSet.FindAsync([id], ct).AsTask();

        public Task<List<T>> ListNoTrackingAsync(CancellationToken ct = default) => _dbSet.AsNoTracking().ToListAsync(ct);

        public Task<List<T>> ListNoTrackingAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => _dbSet.AsNoTracking().Where(predicate).ToListAsync(ct);

        public Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
            => _dbSet.AsNoTracking().CountAsync(predicate, ct);

        public Task<List<T>> ListNoTrackingPagedAsync(
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, int>> orderBy,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var skip = (page - 1) * pageSize;
            return _dbSet.AsNoTracking()
                .Where(predicate)
                .OrderBy(orderBy)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public async Task<int> MaxAsync(Expression<Func<T, int>> selector, CancellationToken ct = default)
        {
            // Cədvəl boşdursa 0, dolu dursa Max rəqəmi qaytarır
            return await _dbSet.Select(selector).OrderByDescending(x => x).FirstOrDefaultAsync(ct);
        }

        public void Remove(T entity) => _dbSet.Remove(entity);
        public void Update(T entity) => _dbSet.Update(entity);

        public Task<List<T>> ListNoTrackingPagedAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, Guid>> orderBy, int page, int pageSize, CancellationToken ct = default)
        {
            var skip = (page - 1) * pageSize;
            return _dbSet.AsNoTracking()
                .Where(predicate)
                .OrderBy(orderBy)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);
        }
    }
}
