using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace EduFlowAI.Admission.Application.Interfaces.Repositories
{
    public interface IGenericRepository<T> where T : class
    {
        /// <summary>
        /// Gets a single entity based on a condition (predicate).
        /// The include parameter is optional.
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> predicate,
            Func<IQueryable<T>, IQueryable<T>>? include = null);

        /// <summary>
        /// Get all entities meet the condition.
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="include"></param>
        /// <returns></returns>
        Task<List<T>?> GetAllAsync(Expression<Func<T, bool>> predicate,
            Func<IQueryable<T>, IQueryable<T>>? include = null);

        /// <summary>
        /// Adds a new entity.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task AddAsync(T entity);

        /// <summary>
        /// Update an existing entity.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        void Update(T entity);

        /// <summary>
        /// Saves changes to the database.
        /// </summary>
        /// <returns></returns>
        Task SaveChangesAsync();
    }
}
