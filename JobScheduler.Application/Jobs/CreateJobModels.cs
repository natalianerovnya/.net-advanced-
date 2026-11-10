using JobScheduler.Domain.Enums;

namespace JobScheduler.Application.Jobs;

public sealed record CreateJobRequest(
    Guid UserId,
    string Name,
    string? Description,
    ScheduleType ScheduleType,
    string ScheduleExpression,
    ConcurrencyMode ConcurrencyMode = ConcurrencyMode.Singleton,
    string Configuration = "{}",
    string? CorrelationId = null);

public enum CreateJobError
{
    None,
    ValidationFailed,
    UserNotFound,
    UserInactive,
    DuplicateName,
    SnapshotFailed
}

public sealed record CreateJobResult(
    Guid? JobId,
    CreateJobError Error,
    IReadOnlyList<string> Messages,
    string CorrelationId,
    bool CompensationFailed = false)
{
    public bool Succeeded => Error == CreateJobError.None;
}
