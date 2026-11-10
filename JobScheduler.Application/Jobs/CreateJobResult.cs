namespace JobScheduler.Application.Jobs;

public sealed record CreateJobResult(
    Guid? JobId,
    CreateJobError Error,
    IReadOnlyList<string> Messages,
    string CorrelationId,
    bool CompensationFailed = false)
{
    public bool Succeeded => Error == CreateJobError.None;
}
