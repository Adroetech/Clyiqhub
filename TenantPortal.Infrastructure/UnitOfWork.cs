using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using TenantPortal.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<Type, object> _repositories = new();
    private IDbContextTransaction? _tx;

    public UnitOfWork(AppDbContext context, ILoggerFactory loggerFactory)
    {
        _context = context;
        _loggerFactory = loggerFactory;
    }


    public IRepository<T> Repository<T>() where T : class
    {
        var type = typeof(T);
        if (!_repositories.ContainsKey(type))
        {
            var repositoryType = typeof(Repository<>).MakeGenericType(type);

            // ✅ Pass AppDbContext + ILoggerFactory (matches Repository<T> constructor now)
            var repositoryInstance = Activator.CreateInstance(repositoryType, _context, _loggerFactory);

            _repositories.Add(type, repositoryInstance!);
        }

        return (IRepository<T>)_repositories[type];
    }


    public async Task<int> CompleteAsync() => await _context.SaveChangesAsync();

    public async Task BeginTransactionAsync()
    {
        _tx = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_tx != null) await _tx.CommitAsync();
    }

    public async Task RollbackTransactionAsync()
    {
        if (_tx != null) await _tx.RollbackAsync();
    }

    public void Dispose()
    {
        _tx?.Dispose();
        _context.Dispose();
    }
}