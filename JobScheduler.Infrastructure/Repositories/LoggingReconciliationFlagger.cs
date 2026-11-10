using JobScheduler.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace JobScheduler.Infrastructure.Repositories;

public sealed class LoggingReconciliationFlagger(ILogger<LoggingReconciliationFlagger> logger) : IReconciliationFlagger
{
    public Task FlagAsync(Guid jobId, string correlationId, string reason, CancellationToken cancellationToken = default)
    {
        logger.LogCritical("RECONCILIATION REQUIRED for job {JobId}: {Reason} CorrelationId={CorrelationId}",
            jobId, reason, correlationId);
        return Task.CompletedTask;
    }
}
