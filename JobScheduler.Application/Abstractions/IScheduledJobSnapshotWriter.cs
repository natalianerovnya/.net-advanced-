using JobScheduler.Domain;

namespace JobScheduler.Application.Abstractions;

public interface IScheduledJobSnapshotWriter
{
    Task AddAsync(ScheduledJobSnapshot snapshot, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid jobId, CancellationToken cancellationToken = default);
}
