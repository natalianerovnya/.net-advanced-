using JobScheduler.Domain;

namespace JobScheduler.Application.Abstractions;

public interface IUserReader
{
    Task<User?> GetByIdAsync(Guid userId, ReadConsistency consistency, CancellationToken cancellationToken = default);
}
