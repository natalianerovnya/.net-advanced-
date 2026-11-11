using Dapper;
using JobScheduler.Application.Abstractions;
using JobScheduler.Domain;
using JobScheduler.Infrastructure.Data;

namespace JobScheduler.Infrastructure.Repositories;

public sealed class ScheduledJobSnapshotRepository(IDbExecutor db) : IScheduledJobSnapshotReader, IScheduledJobSnapshotWriter
{
    private const string SelectByJobId = """
        SELECT JobId, ScheduleType, ScheduleExpression, NextExecutionAt, JobVersion, IsEnabled, Configuration,
               ConcurrencyMode, SyncedAt
        FROM dbo.ScheduledJobSnapshots
        WHERE JobId = @JobId;
        """;

    private const string Insert = """
        INSERT INTO dbo.ScheduledJobSnapshots (JobId, ScheduleType, ScheduleExpression, NextExecutionAt, JobVersion,
                                               IsEnabled, Configuration, ConcurrencyMode, SyncedAt)
        VALUES (@JobId, @ScheduleType, @ScheduleExpression, @NextExecutionAt, @JobVersion,
                @IsEnabled, @Configuration, @ConcurrencyMode, @SyncedAt);
        """;

    private const string Delete = "DELETE FROM dbo.ScheduledJobSnapshots WHERE JobId = @JobId;";

    static ScheduledJobSnapshotRepository() => DapperConfiguration.Register();

    public Task<ScheduledJobSnapshot?> GetByJobIdAsync(Guid jobId, ReadConsistency consistency, CancellationToken cancellationToken = default) =>
        db.ReadAsync<ScheduledJobSnapshot?>(Database.Schedule, consistency, (conn, ct) =>
            conn.QuerySingleOrDefaultAsync<ScheduledJobSnapshot>(new CommandDefinition(SelectByJobId, new { JobId = jobId }, cancellationToken: ct))!,
            cancellationToken);

    public async Task AddAsync(ScheduledJobSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        // Dapper ignores enum type handlers when writing parameters, so enums are passed explicitly as names.
        var parameters = new
        {
            snapshot.JobId,
            ScheduleType = snapshot.ScheduleType.ToString(),
            snapshot.ScheduleExpression,
            snapshot.NextExecutionAt,
            snapshot.JobVersion,
            snapshot.IsEnabled,
            snapshot.Configuration,
            ConcurrencyMode = snapshot.ConcurrencyMode.ToString(),
            snapshot.SyncedAt,
        };

        await db.WriteAsync(Database.Schedule, async (conn, ct) =>
            await conn.ExecuteAsync(new CommandDefinition(Insert, parameters, cancellationToken: ct)),
            cancellationToken);
    }

    public async Task DeleteAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        await db.WriteAsync(Database.Schedule, async (conn, ct) =>
            await conn.ExecuteAsync(new CommandDefinition(Delete, new { JobId = jobId }, cancellationToken: ct)),
            cancellationToken);
}
