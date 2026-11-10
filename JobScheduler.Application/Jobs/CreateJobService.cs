using JobScheduler.Application.Abstractions;
using JobScheduler.Domain;
using JobScheduler.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace JobScheduler.Application.Jobs;

public sealed class CreateJobService(
    IUserReader users,
    IJobReader jobReader,
    IJobWriter jobWriter,
    IScheduledJobSnapshotWriter snapshotWriter,
    IReconciliationFlagger reconciliation,
    TimeProvider timeProvider,
    ILogger<CreateJobService> logger)
{
    public async Task<CreateJobResult> CreateAsync(CreateJobRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : request.CorrelationId;
        var now = timeProvider.GetUtcNow();

        var (errors, expression, nextExecutionAt) = CreateJobValidator.Validate(request, now);
        if (errors.Count > 0)
        {
            return Fail(CreateJobError.ValidationFailed, correlationId, errors.ToArray());
        }

        var name = request.Name.Trim();

        // Eventual is enough for the owner check; the duplicate check must see the latest committed state.
        var user = await users.GetByIdAsync(request.UserId, ReadConsistency.Eventual, cancellationToken);
        if (user is null)
        {
            return Fail(CreateJobError.UserNotFound, correlationId, $"User '{request.UserId}' was not found.");
        }

        if (!user.IsActive)
        {
            return Fail(CreateJobError.UserInactive, correlationId, $"User '{request.UserId}' is not active.");
        }

        if (await jobReader.ExistsByNameAsync(request.UserId, name, ReadConsistency.Strong, cancellationToken))
        {
            return Fail(CreateJobError.DuplicateName, correlationId, $"A job named '{name}' already exists.");
        }

        var jobId = Guid.NewGuid();
        var job = new Job
        {
            JobId = jobId,
            UserId = request.UserId,
            Name = name,
            Description = request.Description,
            SchedulerType = request.ScheduleType,
            ScheduleExpression = expression!,
            NextExecutionAtUtc = nextExecutionAt,
            Status = JobStatus.Active,
            ConcurrencyMode = request.ConcurrencyMode,
            Configuration = request.Configuration,
            JobVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        try
        {
            await jobWriter.AddAsync(job, cancellationToken);
        }
        catch (DuplicateJobNameException)
        {
            return Fail(CreateJobError.DuplicateName, correlationId, $"A job named '{name}' already exists.");
        }

        var snapshot = new ScheduledJobSnapshot
        {
            JobId = jobId,
            ScheduleType = job.SchedulerType,
            ScheduleExpression = job.ScheduleExpression,
            NextExecutionAt = job.NextExecutionAtUtc,
            JobVersion = job.JobVersion,
            IsEnabled = true,
            Configuration = job.Configuration,
            ConcurrencyMode = job.ConcurrencyMode,
            SyncedAt = now
        };

        try
        {
            await snapshotWriter.AddAsync(snapshot, cancellationToken);
        }
        catch (Exception snapshotError)
        {
            logger.LogError(snapshotError,
                "Snapshot write failed for job {JobId}; compensating. CorrelationId={CorrelationId}", jobId, correlationId);
            var compensationFailed = await CompensateAsync(jobId, correlationId, snapshotError);
            return new CreateJobResult(null, CreateJobError.SnapshotFailed,
                ["The job could not be scheduled and was not created."], correlationId, compensationFailed);
        }

        logger.LogInformation("Job {JobId} created for user {UserId}. CorrelationId={CorrelationId}",
            jobId, request.UserId, correlationId);
        return new CreateJobResult(jobId, CreateJobError.None, [], correlationId);
    }

    // Compensation must run even if the caller cancelled the request. Returns true if it failed.
    private async Task<bool> CompensateAsync(Guid jobId, string correlationId, Exception snapshotError)
    {
        try
        {
            await jobWriter.DeleteAsync(jobId, CancellationToken.None);
            logger.LogWarning("Compensating delete of job {JobId} succeeded. CorrelationId={CorrelationId}",
                jobId, correlationId);
            return false;
        }
        catch (Exception deleteError)
        {
            logger.LogCritical(deleteError,
                "Compensating delete of job {JobId} FAILED; job needs reconciliation. CorrelationId={CorrelationId}",
                jobId, correlationId);
            try
            {
                await reconciliation.FlagAsync(jobId, correlationId,
                    $"Snapshot write failed ({snapshotError.GetType().Name}) and compensating delete failed ({deleteError.GetType().Name}).",
                    CancellationToken.None);
            }
            catch (Exception flagError)
            {
                logger.LogCritical(flagError,
                    "Could not flag job {JobId} for reconciliation. CorrelationId={CorrelationId}", jobId, correlationId);
            }

            return true;
        }
    }

    private static CreateJobResult Fail(CreateJobError error, string correlationId, params string[] messages) =>
        new(null, error, messages, correlationId);
}
