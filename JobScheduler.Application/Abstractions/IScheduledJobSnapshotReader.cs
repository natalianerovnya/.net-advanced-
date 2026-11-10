using JobScheduler.Domain;

namespace JobScheduler.Application.Abstractions;

public interface IScheduledJobSnapshotReader
{
    Task<ScheduledJobSnapshot?> GetByJobIdAsync(Guid jobId, ReadConsistency consistency, CancellationToken cancellationToken = default);
}
