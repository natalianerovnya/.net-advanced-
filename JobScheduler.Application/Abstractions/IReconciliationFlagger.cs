namespace JobScheduler.Application.Abstractions;

public interface IReconciliationFlagger
{
    Task FlagAsync(Guid jobId, string correlationId, string reason, CancellationToken cancellationToken = default);
}
