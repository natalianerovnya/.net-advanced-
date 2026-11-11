using JobScheduler.Application.Jobs;
using JobScheduler.Domain.Enums;

namespace JobScheduler.Tests.Unit.Application;

internal sealed class CreateJobRequestBuilder
{
    public Guid UserId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public string Name { get; set; } = "Nightly report";

    public string? Description { get; set; } = "Builds the nightly report";

    public ScheduleType ScheduleType { get; set; } = ScheduleType.Cron;

    public string ScheduleExpression { get; set; } = "0 2 * * *";

    public ConcurrencyMode ConcurrencyMode { get; set; } = ConcurrencyMode.Singleton;

    public string Configuration { get; set; } = "{\"target\":\"reports\"}";

    public string? CorrelationId { get; set; }

    public CreateJobRequest Build() => new(
        UserId,
        Name,
        Description,
        ScheduleType,
        ScheduleExpression,
        ConcurrencyMode,
        Configuration,
        CorrelationId);
}
