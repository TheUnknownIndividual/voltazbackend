using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Interfaces;
using Volt.Infrastructure.Data;
using Volt.Infrastructure.Repositories;

namespace Volt.Infrastructure.UOW
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly DataContext _context;
        private readonly ConcurrentDictionary<Type, object> _repositories = new();

        public UnitOfWork(DataContext context)
        {
            _context = context;
        }

        public IRepository<TEntity> Repository<TEntity>() where TEntity : class
        {
            var type = typeof(TEntity);
            var repo = _repositories.GetOrAdd(type, _ => new GenericRepository<TEntity>(_context));
            return (IRepository<TEntity>)repo;
        }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
