using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace JobScheduler.Infrastructure.Data;

public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly JobDbOptions _jobDb;
    private readonly ScheduleDbOptions _scheduleDb;
    private readonly SqlRetryLogicBaseProvider _retryProvider;

    public SqlConnectionFactory(IOptions<JobDbOptions> jobDb, IOptions<ScheduleDbOptions> scheduleDb)
    {
        _jobDb = jobDb.Value;
        _scheduleDb = scheduleDb.Value;
        _retryProvider = SqlConfigurableRetryFactory.CreateExponentialRetryProvider(new SqlRetryLogicOption
        {
            NumberOfTries = 5,
            DeltaTime = TimeSpan.FromSeconds(1),
            MaxTimeInterval = TimeSpan.FromSeconds(15)
        });
    }

    public DbConnection Create(Database database, Intent intent)
    {
        var options = database switch
        {
            Database.Job => (DatabaseOptions)_jobDb,
            Database.Schedule => _scheduleDb,
            _ => throw new ArgumentOutOfRangeException(nameof(database))
        };

        var connectionString = intent == Intent.Write ? options.Primary : options.Replica;
        return new SqlConnection(connectionString) { RetryLogicProvider = _retryProvider };
    }
}
