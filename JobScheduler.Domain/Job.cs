using JobScheduler.Domain.Enums;

namespace JobScheduler.Domain;

public class Job
{
    public Guid JobId { get; init; }

    public Guid UserId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public ScheduleType SchedulerType { get; init; }

    public required string ScheduleExpression { get; init; }

    public DateTimeOffset? NextExecutionAtUtc { get; init; }

    public JobStatus Status { get; init; }

    public ConcurrencyMode ConcurrencyMode { get; init; }

    public required string Configuration { get; init; }

    public int JobVersion { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? DeletedAt { get; init; }
}
