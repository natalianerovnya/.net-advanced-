using JobScheduler.Domain;
using JobScheduler.Domain.Enums;

namespace JobScheduler.Api.Contracts;

public sealed record JobResponse(
    Guid JobId,
    Guid UserId,
    string Name,
    string? Description,
    ScheduleType ScheduleType,
    string ScheduleExpression,
    DateTimeOffset? NextExecutionAt,
    JobStatus Status,
    ConcurrencyMode ConcurrencyMode,
    string Configuration,
    int JobVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static JobResponse From(Job job) => new(
        job.JobId,
        job.UserId,
        job.Name,
        job.Description,
        job.SchedulerType,
        job.ScheduleExpression,
        job.NextExecutionAtUtc,
        job.Status,
        job.ConcurrencyMode,
        job.Configuration,
        job.JobVersion,
        job.CreatedAt,
        job.UpdatedAt);
}
