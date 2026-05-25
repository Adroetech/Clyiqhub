using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using TenantPortal.Infrastructure.Persistence;

public class Repository<T> : IRepository<T> where T : class
{
    //protected readonly AppDbContext _context;
    //protected readonly DbSet<T> _dbSet;
    //protected readonly ILogger _logger;


    //public Repository(AppDbContext context, ILogger<Repository<T>> logger)
    //{
    //    _context = context;
    //    _logger = logger;
    //    _dbSet = _context.Set<T>();
    //}

    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;
    protected readonly ILogger _logger;

    // ✅ IMPORTANT: this constructor MUST match UnitOfWork Activator.CreateInstance parameters
    public Repository(AppDbContext context, ILoggerFactory loggerFactory)
    {
        _context = context;
        _dbSet = _context.Set<T>();
        _logger = loggerFactory.CreateLogger(typeof(Repository<T>));
    }


    public async Task<IEnumerable<T>> GetAllAsync()
        => await _dbSet.AsNoTracking().ToListAsync();

    public async Task<IEnumerable<T>> FindAllAsync(Expression<Func<T, bool>> match)
        => await _dbSet.AsNoTracking().Where(match).ToListAsync();

    public async Task<T?> FindAsync(Expression<Func<T, bool>> match, bool trackChanges = false)
    {
        IQueryable<T> q = trackChanges ? _dbSet : _dbSet.AsNoTracking();
        return await q.FirstOrDefaultAsync(match);
    }

    public async Task<T?> GetByIdAsync(Guid id)
        => await _dbSet.FindAsync(id);

    public async Task AddAsync(T entity)
        => await _dbSet.AddAsync(entity);

    public Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _dbSet.FindAsync(id);
        if (entity == null)
            throw new KeyNotFoundException($"{typeof(T).Name} not found");

        _dbSet.Remove(entity);
    }

    public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
        => await _dbSet.AsNoTracking().AnyAsync(predicate);

    public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
        => predicate == null
            ? await _dbSet.AsNoTracking().CountAsync()
            : await _dbSet.AsNoTracking().CountAsync(predicate);

    public async Task<IEnumerable<T>> FindWithFiltersAsync(
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        int? skip = null,
        int? take = null)
    {
        IQueryable<T> q = _dbSet.AsNoTracking();

        if (predicate != null) q = q.Where(predicate);
        if (orderBy != null) q = orderBy(q);
        if (skip.HasValue) q = q.Skip(skip.Value);
        if (take.HasValue) q = q.Take(take.Value);

        return await q.ToListAsync();
    }
}