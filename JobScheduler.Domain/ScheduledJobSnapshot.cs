using JobScheduler.Domain.Enums;

namespace JobScheduler.Domain;

public class ScheduledJobSnapshot
{
    public Guid JobId { get; init; }

    public ScheduleType ScheduleType { get; init; }

    public required string ScheduleExpression { get; init; }

    public DateTimeOffset? NextExecutionAt { get; init; }

    public int JobVersion { get; init; }

    public bool IsEnabled { get; init; }

    public required string Configuration { get; init; }

    public ConcurrencyMode ConcurrencyMode { get; init; }

    public DateTimeOffset SyncedAt { get; init; }
}
