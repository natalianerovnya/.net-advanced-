using JobScheduler.Domain;
using JobScheduler.Domain.Enums;

namespace JobScheduler.Api.Contracts;

public sealed record ScheduledJobSnapshotResponse(
    Guid JobId,
    ScheduleType ScheduleType,
    string ScheduleExpression,
    DateTimeOffset? NextExecutionAt,
    int JobVersion,
    bool IsEnabled,
    string Configuration,
    ConcurrencyMode ConcurrencyMode,
    DateTimeOffset SyncedAt)
{
    public static ScheduledJobSnapshotResponse From(ScheduledJobSnapshot snapshot) => new(
        snapshot.JobId,
        snapshot.ScheduleType,
        snapshot.ScheduleExpression,
        snapshot.NextExecutionAt,
        snapshot.JobVersion,
        snapshot.IsEnabled,
        snapshot.Configuration,
        snapshot.ConcurrencyMode,
        snapshot.SyncedAt);
}
