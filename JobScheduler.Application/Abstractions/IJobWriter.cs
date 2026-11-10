using JobScheduler.Domain;

namespace JobScheduler.Application.Abstractions;

public interface IJobWriter
{
    Task AddAsync(Job job, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid jobId, CancellationToken cancellationToken = default);
}
