using EduFlowAI.Admission.Application.DbContextAbstraction;
using EduFlowAI.Admission.Application.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace EduFlowAI.Admission.Infrastructure.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        private readonly IAdmissionDbContext _dbContext;
        protected readonly DbSet<T> _dbSet;

        public GenericRepository(IAdmissionDbContext dbContext)
        {
            _dbContext = dbContext;
            _dbSet = ((DbContext)_dbContext).Set<T>();
        }

        public async Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> predicate,
            Func<IQueryable<T>, IQueryable<T>>? include = null)
        {
            IQueryable<T> query = _dbSet;

            // Apply includes if provided
            if (include != null)
            {
                query = include(query);
            }

            return await query.FirstOrDefaultAsync(predicate);
        }

        public async Task<T?> GetFirstOrDefaultWithIncludeAsync(Expression<Func<T, bool>> predicate,
            bool asNoTracking = false,
            CancellationToken cancellationToken = default,
            params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _dbSet;

            if (asNoTracking)
            {
                query = query.AsNoTracking();
            }

            foreach (var include in includes)
            {
                query = query.Include(include);
            }

            return await query.FirstOrDefaultAsync(
                predicate,
                cancellationToken);
        }

        public async Task<List<T>?> GetAllAsync(Expression<Func<T, bool>> predicate,
            Func<IQueryable<T>, IQueryable<T>>? include = null)
        {
            IQueryable<T> query = _dbSet;

            // Apply includes if provided
            if (include != null)
            {
                query = include(query);
            }

            return await query.Where(predicate).ToListAsync();
        }

        public async Task<(List<T> Data, int TotalCount)> GetPagedAsync(
            Expression<Func<T, bool>> predicate, int page,
            int pageSize, Func<IQueryable<T>, IQueryable<T>>? include = null)
        {
            IQueryable<T> query = _dbSet.Where(predicate);

            // Get total count before pagination
            int totalCount = await query.CountAsync();

            // Apply includes and AsNoTracking if provided
            if (include != null)
            {
                query = include(query);
            }

            // Apply SQL-level pagination
            var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return (data, totalCount);
        }

        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public void Update(T entity)
        {
            _dbSet.Update(entity);
        }

        public void Delete(T entity)
        {
            _dbSet.Remove(entity);
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
