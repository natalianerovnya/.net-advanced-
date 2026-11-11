using JobScheduler.Api.Contracts;
using JobScheduler.Application.Abstractions;
using JobScheduler.Application.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace JobScheduler.Api.Controllers;

[ApiController]
[Route("api/jobs")]
[Produces("application/json")]
public sealed class JobsController(
    ICreateJobService createJobService,
    IJobReader jobReader,
    IScheduledJobSnapshotReader snapshotReader) : ControllerBase
{
    public const string CorrelationIdHeader = "X-Correlation-Id";

    /// <summary>Creates a job and its schedule snapshot (UC1.1).</summary>
    [HttpPost]
    [ProducesResponseType<CreateJobResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateJobRequestDto body, CancellationToken cancellationToken)
    {
        var correlationId = Request.Headers[CorrelationIdHeader].FirstOrDefault();

        var request = new CreateJobRequest(
            body.UserId,
            body.Name,
            body.Description,
            body.ScheduleType,
            body.ScheduleExpression,
            body.ConcurrencyMode,
            body.Configuration ?? "{}",
            correlationId);

        var result = await createJobService.CreateAsync(request, cancellationToken);
        Response.Headers[CorrelationIdHeader] = result.CorrelationId;

        if (result.Succeeded)
        {
            var jobId = result.JobId!.Value;
            return CreatedAtRoute(
                "GetJob",
                new { jobId },
                new CreateJobResponse(jobId, result.CorrelationId));
        }

        return ToProblem(result);
    }

    /// <summary>
    /// Gets a job. Eventual (default) may read from the geo-replica and be briefly stale;
    /// use Strong right after creating a job.
    /// </summary>
    [HttpGet("{jobId:guid}", Name = "GetJob")]
    [ProducesResponseType<JobResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(
        Guid jobId,
        [FromQuery] ReadConsistency consistency = ReadConsistency.Eventual,
        CancellationToken cancellationToken = default)
    {
        var job = await jobReader.GetByIdAsync(jobId, consistency, cancellationToken);
        if (job is null || job.DeletedAt is not null)
        {
            return NotFound(NewProblem(StatusCodes.Status404NotFound, "Job not found", $"Job '{jobId}' was not found."));
        }

        return Ok(JobResponse.From(job));
    }

    /// <summary>Gets the orchestrator's schedule snapshot for a job. Same consistency semantics as the job lookup.</summary>
    [HttpGet("{jobId:guid}/snapshot")]
    [ProducesResponseType<ScheduledJobSnapshotResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSnapshotAsync(
        Guid jobId,
        [FromQuery] ReadConsistency consistency = ReadConsistency.Eventual,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await snapshotReader.GetByJobIdAsync(jobId, consistency, cancellationToken);
        if (snapshot is null)
        {
            return NotFound(NewProblem(StatusCodes.Status404NotFound, "Snapshot not found", $"No snapshot exists for job '{jobId}'."));
        }

        return Ok(ScheduledJobSnapshotResponse.From(snapshot));
    }

    private static ProblemDetails NewProblem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
    };

    private ObjectResult ToProblem(CreateJobResult result)
    {
        var (status, title) = result.Error switch
        {
            CreateJobError.ValidationFailed => (StatusCodes.Status400BadRequest, "Validation failed"),
            CreateJobError.UserNotFound => (StatusCodes.Status404NotFound, "User not found"),
            CreateJobError.UserInactive => (StatusCodes.Status409Conflict, "User is inactive"),
            CreateJobError.DuplicateName => (StatusCodes.Status409Conflict, "Duplicate job name"),
            CreateJobError.SnapshotFailed => (StatusCodes.Status503ServiceUnavailable, "Job could not be scheduled"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error"),
        };

        var problem = NewProblem(status, title, string.Join(" ", result.Messages));
        problem.Extensions["correlationId"] = result.CorrelationId;
        problem.Extensions["errors"] = result.Messages;
        if (result.CompensationFailed)
        {
            problem.Extensions["reconciliationRequired"] = true;
        }

        return new ObjectResult(problem) { StatusCode = status };
    }
}
