using System.Data.Common;
using JobScheduler.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace JobScheduler.Infrastructure.Data;

public sealed class DbExecutor(IDbConnectionFactory factory, ILogger<DbExecutor> logger) : IDbExecutor
{
    public Task<T> WriteAsync<T>(Database database, Func<DbConnection, CancellationToken, Task<T>> work, CancellationToken cancellationToken) =>
        RunAsync(database, Intent.Write, work, cancellationToken);

    public async Task<T> ReadAsync<T>(Database database, ReadConsistency consistency, Func<DbConnection, CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        if (consistency == ReadConsistency.Strong)
            return await RunAsync(database, Intent.Write, work, cancellationToken);

        try
        {
            return await RunAsync(database, Intent.Read, work, cancellationToken);
        }
        catch (DbException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Replica read on {Database} failed; falling back to the primary.", database);
            return await RunAsync(database, Intent.Write, work, cancellationToken);
        }
    }

    private async Task<T> RunAsync<T>(Database database, Intent intent, Func<DbConnection, CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        await using var connection = factory.Create(database, intent);
        await connection.OpenAsync(cancellationToken);
        return await work(connection, cancellationToken);
    }
}
