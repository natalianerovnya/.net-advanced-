using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using JobScheduler.Infrastructure.Data;
using DbKind = JobScheduler.Infrastructure.Data.Database;

namespace JobScheduler.Tests.TestDoubles;

/// <summary>A connection that opens (or fails to open) without touching a database.</summary>
internal sealed class FakeDbConnection(DbKind target, Intent intent, bool failOnOpen) : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;

    public DbKind Target { get; } = target;

    public Intent Intent { get; } = intent;

    [AllowNull]
    public override string ConnectionString { get; set; } = string.Empty;

    public override string Database => Target.ToString();

    public override string DataSource => Intent.ToString();

    public override string ServerVersion => "fake";

    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName)
    {
    }

    public override void Close() => _state = ConnectionState.Closed;

    public override void Open()
    {
        if (failOnOpen)
        {
            throw new FakeDbException($"Cannot open {Intent} connection.");
        }

        _state = ConnectionState.Open;
    }

    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        Open();
        return Task.CompletedTask;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();

    protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
}
