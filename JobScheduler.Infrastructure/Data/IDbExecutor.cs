using System.Data.Common;
using JobScheduler.Application.Abstractions;

namespace JobScheduler.Infrastructure.Data;

public interface IDbExecutor
{
    Task<T> WriteAsync<T>(Database database, Func<DbConnection, CancellationToken, Task<T>> work, CancellationToken cancellationToken);

    Task<T> ReadAsync<T>(Database database, ReadConsistency consistency, Func<DbConnection, CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}
