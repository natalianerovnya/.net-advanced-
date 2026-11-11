namespace JobScheduler.Application.Jobs;

/// <summary>UC1.1: create a new job and its schedule snapshot.</summary>
public interface ICreateJobService
{
    Task<CreateJobResult> CreateAsync(CreateJobRequest request, CancellationToken cancellationToken = default);
}
