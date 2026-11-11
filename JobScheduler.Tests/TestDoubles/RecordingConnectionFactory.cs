using System.Data.Common;
using JobScheduler.Infrastructure.Data;
using DbKind = JobScheduler.Infrastructure.Data.Database;

namespace JobScheduler.Tests.TestDoubles;

internal sealed class RecordingConnectionFactory : IDbConnectionFactory
{
    public List<(DbKind Database, Intent Intent)> Calls { get; } = [];

    public bool ReplicaFailsOnOpen { get; set; }

    public bool PrimaryFailsOnOpen { get; set; }

    public DbConnection Create(DbKind database, Intent intent)
    {
        Calls.Add((database, intent));
        var fail = intent == Intent.Read ? ReplicaFailsOnOpen : PrimaryFailsOnOpen;
        return new FakeDbConnection(database, intent, fail);
    }
}
