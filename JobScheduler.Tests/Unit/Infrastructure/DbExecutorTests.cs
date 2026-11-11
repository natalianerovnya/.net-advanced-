using System.Data.Common;
using JobScheduler.Application.Abstractions;
using JobScheduler.Infrastructure.Data;
using JobScheduler.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using DbKind = JobScheduler.Infrastructure.Data.Database;

namespace JobScheduler.Tests.Unit.Infrastructure;

public class DbExecutorTests
{
    private readonly RecordingConnectionFactory _factory = new();
    private readonly DbExecutor _executor;

    public DbExecutorTests()
    {
        _executor = new DbExecutor(_factory, NullLogger<DbExecutor>.Instance);
    }

    private static Task<Intent> UsedIntent(DbConnection connection, CancellationToken cancellationToken) =>
        Task.FromResult(((FakeDbConnection)connection).Intent);

    [Theory]
    [InlineData(DbKind.Job)]
    [InlineData(DbKind.Schedule)]
    public async Task Write_UsesPrimary(DbKind database)
    {
        var used = await _executor.WriteAsync(database, UsedIntent, CancellationToken.None);

        Assert.Equal(Intent.Write, used);
        Assert.Equal([(database, Intent.Write)], _factory.Calls);
    }

    [Fact]
    public async Task StrongRead_UsesPrimary()
    {
        var used = await _executor.ReadAsync(DbKind.Job, ReadConsistency.Strong, UsedIntent, CancellationToken.None);

        Assert.Equal(Intent.Write, used);
        Assert.Equal([(DbKind.Job, Intent.Write)], _factory.Calls);
    }

    [Fact]
    public async Task EventualRead_UsesReplica()
    {
        var used = await _executor.ReadAsync(DbKind.Schedule, ReadConsistency.Eventual, UsedIntent, CancellationToken.None);

        Assert.Equal(Intent.Read, used);
        Assert.Equal([(DbKind.Schedule, Intent.Read)], _factory.Calls);
    }

    [Fact]
    public async Task EventualRead_FallsBackToPrimary_WhenReplicaCannotConnect()
    {
        _factory.ReplicaFailsOnOpen = true;

        var used = await _executor.ReadAsync(DbKind.Job, ReadConsistency.Eventual, UsedIntent, CancellationToken.None);

        Assert.Equal(Intent.Write, used);
        Assert.Equal([(DbKind.Job, Intent.Read), (DbKind.Job, Intent.Write)], _factory.Calls);
    }

    [Fact]
    public async Task EventualRead_FallsBackToPrimary_WhenQueryOnReplicaFails()
    {
        var result = await _executor.ReadAsync(
            DbKind.Job,
            ReadConsistency.Eventual,
            (conn, ct) => ((FakeDbConnection)conn).Intent == Intent.Read
                ? throw new FakeDbException("replica query failed")
                : Task.FromResult("from primary"),
            CancellationToken.None);

        Assert.Equal("from primary", result);
        Assert.Equal([(DbKind.Job, Intent.Read), (DbKind.Job, Intent.Write)], _factory.Calls);
    }

    [Fact]
    public async Task EventualRead_PropagatesError_WhenPrimaryFallbackAlsoFails()
    {
        _factory.ReplicaFailsOnOpen = true;
        _factory.PrimaryFailsOnOpen = true;

        await Assert.ThrowsAsync<FakeDbException>(
            () => _executor.ReadAsync(DbKind.Job, ReadConsistency.Eventual, UsedIntent, CancellationToken.None));

        Assert.Equal(2, _factory.Calls.Count);
    }

    [Fact]
    public async Task StrongRead_DoesNotFallBack_WhenPrimaryFails()
    {
        _factory.PrimaryFailsOnOpen = true;

        await Assert.ThrowsAsync<FakeDbException>(
            () => _executor.ReadAsync(DbKind.Job, ReadConsistency.Strong, UsedIntent, CancellationToken.None));

        Assert.Equal([(DbKind.Job, Intent.Write)], _factory.Calls);
    }

    [Fact]
    public async Task EventualRead_DoesNotFallBack_OnNonDatabaseErrors()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _executor.ReadAsync<int>(
                DbKind.Job,
                ReadConsistency.Eventual,
                (conn, ct) => throw new InvalidOperationException("bug"),
                CancellationToken.None));

        Assert.Equal([(DbKind.Job, Intent.Read)], _factory.Calls);
    }

    [Fact]
    public async Task EventualRead_DoesNotFallBack_WhenCancelled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _factory.ReplicaFailsOnOpen = true;

        await Assert.ThrowsAsync<FakeDbException>(
            () => _executor.ReadAsync(DbKind.Job, ReadConsistency.Eventual, UsedIntent, cts.Token));

        Assert.Equal([(DbKind.Job, Intent.Read)], _factory.Calls);
    }
}
