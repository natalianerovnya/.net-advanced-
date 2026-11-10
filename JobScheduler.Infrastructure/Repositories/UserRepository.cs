using Dapper;
using JobScheduler.Application.Abstractions;
using JobScheduler.Domain;
using JobScheduler.Infrastructure.Data;

namespace JobScheduler.Infrastructure.Repositories;

public sealed class UserRepository(IDbExecutor db) : IUserReader
{
    private const string SelectById = """
        SELECT UserId, Name, Email, CreatedAt, IsActive
        FROM dbo.Users
        WHERE UserId = @UserId;
        """;

    static UserRepository() => DapperConfiguration.Register();

    public Task<User?> GetByIdAsync(Guid userId, ReadConsistency consistency, CancellationToken cancellationToken = default) =>
        db.ReadAsync<User?>(Database.Job, consistency, (conn, ct) =>
            conn.QuerySingleOrDefaultAsync<User>(new CommandDefinition(SelectById, new { UserId = userId }, cancellationToken: ct))!,
            cancellationToken);
}
