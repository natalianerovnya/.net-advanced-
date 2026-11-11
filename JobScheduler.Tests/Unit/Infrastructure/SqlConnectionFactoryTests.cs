using JobScheduler.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using DbKind = JobScheduler.Infrastructure.Data.Database;

namespace JobScheduler.Tests.Unit.Infrastructure;

public class SqlConnectionFactoryTests
{
    private readonly SqlConnectionFactory _factory = new(
        Options.Create(new JobDbOptions
        {
            Primary = "Server=job-primary;Database=JobDb;",
            Replica = "Server=job-replica;Database=JobDb;ApplicationIntent=ReadOnly;",
        }),
        Options.Create(new ScheduleDbOptions
        {
            Primary = "Server=schedule-primary;Database=ScheduleDb;",
            Replica = "Server=schedule-replica;Database=ScheduleDb;ApplicationIntent=ReadOnly;",
        }));

    [Theory]
    [InlineData(DbKind.Job, Intent.Write, "job-primary", ApplicationIntent.ReadWrite)]
    [InlineData(DbKind.Job, Intent.Read, "job-replica", ApplicationIntent.ReadOnly)]
    [InlineData(DbKind.Schedule, Intent.Write, "schedule-primary", ApplicationIntent.ReadWrite)]
    [InlineData(DbKind.Schedule, Intent.Read, "schedule-replica", ApplicationIntent.ReadOnly)]
    public void Create_PicksConnectionStringByDatabaseAndIntent(
        DbKind database,
        Intent intent,
        string expectedServer,
        ApplicationIntent expectedIntent)
    {
        using var connection = (SqlConnection)_factory.Create(database, intent);

        var builder = new SqlConnectionStringBuilder(connection.ConnectionString);
        Assert.Equal(expectedServer, builder.DataSource);
        Assert.Equal(expectedIntent, builder.ApplicationIntent);
    }

    [Fact]
    public void Create_ReturnsUnopenedConnection()
    {
        using var connection = _factory.Create(DbKind.Job, Intent.Write);

        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
    }
}
