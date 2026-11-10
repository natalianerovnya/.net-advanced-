using Dapper;
using JobScheduler.Application.Abstractions;
using JobScheduler.Domain;
using JobScheduler.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace JobScheduler.Infrastructure.Repositories;

public sealed class JobRepository(IDbExecutor db) : IJobReader, IJobWriter
{
    public const string UniqueNameIndex = "UX_Jobs_UserId_Name_Active";

    private const string SelectById = """
        SELECT JobId, UserId, Name, Description, SchedulerType, ScheduleExpression, NextExecutionAtUtc, Status,
               ConcurrencyMode, Configuration, JobVersion, CreatedAt, UpdatedAt, DeletedAt
        FROM dbo.Jobs
        WHERE JobId = @JobId;
        """;

    private const string ExistsByName = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM dbo.Jobs WHERE UserId = @UserId AND Name = @Name AND DeletedAt IS NULL)
        THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END;
        """;

    private const string Insert = """
        INSERT INTO dbo.Jobs (JobId, UserId, Name, Description, SchedulerType, ScheduleExpression, NextExecutionAtUtc,
                              Status, ConcurrencyMode, Configuration, JobVersion, CreatedAt, UpdatedAt, DeletedAt)
        VALUES (@JobId, @UserId, @Name, @Description, @SchedulerType, @ScheduleExpression, @NextExecutionAtUtc,
                @Status, @ConcurrencyMode, @Configuration, @JobVersion, @CreatedAt, @UpdatedAt, @DeletedAt);
        """;

    private const string Delete = "DELETE FROM dbo.Jobs WHERE JobId = @JobId;";

    static JobRepository() => DapperConfiguration.Register();

    public Task<Job?> GetByIdAsync(Guid jobId, ReadConsistency consistency, CancellationToken cancellationToken = default) =>
        db.ReadAsync<Job?>(Database.Job, consistency, (conn, ct) =>
            conn.QuerySingleOrDefaultAsync<Job>(new CommandDefinition(SelectById, new { JobId = jobId }, cancellationToken: ct))!,
            cancellationToken);

    public Task<bool> ExistsByNameAsync(Guid userId, string name, ReadConsistency consistency, CancellationToken cancellationToken = default) =>
        db.ReadAsync(Database.Job, consistency, (conn, ct) =>
            conn.ExecuteScalarAsync<bool>(new CommandDefinition(ExistsByName, new { UserId = userId, Name = name }, cancellationToken: ct)),
            cancellationToken);

    public async Task AddAsync(Job job, CancellationToken cancellationToken = default)
    {
        try
        {
            await db.WriteAsync(Database.Job, async (conn, ct) =>
                await conn.ExecuteAsync(new CommandDefinition(Insert, job, cancellationToken: ct)),
                cancellationToken);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627 && ex.Message.Contains(UniqueNameIndex, StringComparison.OrdinalIgnoreCase))
        {
            throw new DuplicateJobNameException(job.UserId, job.Name, ex);
        }
    }

    public async Task DeleteAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        await db.WriteAsync(Database.Job, async (conn, ct) =>
            await conn.ExecuteAsync(new CommandDefinition(Delete, new { JobId = jobId }, cancellationToken: ct)),
            cancellationToken);
}
