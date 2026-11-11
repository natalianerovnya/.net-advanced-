using JobScheduler.Domain.Enums;

namespace JobScheduler.Api.Contracts;

public sealed record CreateJobRequestDto(
    Guid UserId,
    string Name,
    string? Description,
    ScheduleType ScheduleType,
    string ScheduleExpression,
    ConcurrencyMode ConcurrencyMode = ConcurrencyMode.Singleton,
    string? Configuration = null);
