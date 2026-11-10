using JobScheduler.Domain;

namespace JobScheduler.Application.Abstractions;

public interface IJobReader
{
    Task<Job?> GetByIdAsync(Guid jobId, ReadConsistency consistency, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(Guid userId, string name, ReadConsistency consistency, CancellationToken cancellationToken = default);
}
