using JobScheduler.Api.Contracts;
using JobScheduler.Api.Controllers;
using JobScheduler.Application.Abstractions;
using JobScheduler.Application.Jobs;
using JobScheduler.Domain;
using JobScheduler.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace JobScheduler.Tests.Unit.Api;

public class JobsControllerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid JobId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset At = new(2030, 1, 1, 2, 0, 0, TimeSpan.Zero);

    private readonly ICreateJobService _createJob = Substitute.For<ICreateJobService>();
    private readonly IJobReader _jobReader = Substitute.For<IJobReader>();
    private readonly IScheduledJobSnapshotReader _snapshotReader = Substitute.For<IScheduledJobSnapshotReader>();
    private readonly JobsController _controller;

    public JobsControllerTests()
    {
        _controller = new JobsController(_createJob, _jobReader, _snapshotReader)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    private static CreateJobRequestDto Body(string name = "Nightly report") =>
        new(UserId, name, "desc", ScheduleType.Cron, "0 2 * * *", ConcurrencyMode.Concurrent, "{\"a\":1}");

    private static CreateJobResult Failure(CreateJobError error, bool compensationFailed = false) =>
        new(null, error, ["something went wrong"], "corr-9", compensationFailed);

    private static Job NewJob(DateTimeOffset? deletedAt = null) => new()
    {
        JobId = JobId,
        UserId = UserId,
        Name = "Nightly report",
        SchedulerType = ScheduleType.Cron,
        ScheduleExpression = "0 2 * * *",
        NextExecutionAtUtc = At,
        Status = JobStatus.Active,
        ConcurrencyMode = ConcurrencyMode.Singleton,
        Configuration = "{}",
        JobVersion = 1,
        CreatedAt = At,
        UpdatedAt = At,
        DeletedAt = deletedAt,
    };

    [Fact]
    public async Task Create_ReturnsCreatedWithLocationAndCorrelationHeader()
    {
        _createJob.CreateAsync(Arg.Any<CreateJobRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateJobResult(JobId, CreateJobError.None, [], "corr-1"));

        var response = await _controller.CreateAsync(Body(), CancellationToken.None);

        var created = Assert.IsType<CreatedAtRouteResult>(response);
        Assert.Equal("GetJob", created.RouteName);
        Assert.Equal(JobId, created.RouteValues!["jobId"]);
        var payload = Assert.IsType<CreateJobResponse>(created.Value);
        Assert.Equal(JobId, payload.JobId);
        Assert.Equal("corr-1", payload.CorrelationId);
        Assert.Equal("corr-1", _controller.Response.Headers[JobsController.CorrelationIdHeader].ToString());
    }

    [Fact]
    public async Task Create_MapsBodyAndCorrelationHeaderToApplicationRequest()
    {
        _controller.Request.Headers[JobsController.CorrelationIdHeader] = "from-client";
        _createJob.CreateAsync(Arg.Any<CreateJobRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateJobResult(JobId, CreateJobError.None, [], "from-client"));

        await _controller.CreateAsync(Body(), CancellationToken.None);

        await _createJob.Received(1).CreateAsync(
            Arg.Is<CreateJobRequest>(r =>
                r.UserId == UserId &&
                r.Name == "Nightly report" &&
                r.Description == "desc" &&
                r.ScheduleType == ScheduleType.Cron &&
                r.ScheduleExpression == "0 2 * * *" &&
                r.ConcurrencyMode == ConcurrencyMode.Concurrent &&
                r.Configuration == "{\"a\":1}" &&
                r.CorrelationId == "from-client"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_DefaultsConfigurationToEmptyJsonObject()
    {
        _createJob.CreateAsync(Arg.Any<CreateJobRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateJobResult(JobId, CreateJobError.None, [], "c"));

        await _controller.CreateAsync(Body() with { Configuration = null }, CancellationToken.None);

        await _createJob.Received(1).CreateAsync(
            Arg.Is<CreateJobRequest>(r => r.Configuration == "{}"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(CreateJobError.ValidationFailed, StatusCodes.Status400BadRequest)]
    [InlineData(CreateJobError.UserNotFound, StatusCodes.Status404NotFound)]
    [InlineData(CreateJobError.UserInactive, StatusCodes.Status409Conflict)]
    [InlineData(CreateJobError.DuplicateName, StatusCodes.Status409Conflict)]
    [InlineData(CreateJobError.SnapshotFailed, StatusCodes.Status503ServiceUnavailable)]
    public async Task Create_MapsFailuresToProblemDetails(CreateJobError error, int expectedStatus)
    {
        _createJob.CreateAsync(Arg.Any<CreateJobRequest>(), Arg.Any<CancellationToken>())
            .Returns(Failure(error));

        var response = await _controller.CreateAsync(Body(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(response);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(expectedStatus, problem.Status);
        Assert.Equal("corr-9", problem.Extensions["correlationId"]);
        Assert.False(problem.Extensions.ContainsKey("reconciliationRequired"));
        Assert.Equal("corr-9", _controller.Response.Headers[JobsController.CorrelationIdHeader].ToString());
    }

    [Fact]
    public async Task Create_FlagsReconciliationWhenCompensationFailed()
    {
        _createJob.CreateAsync(Arg.Any<CreateJobRequest>(), Arg.Any<CancellationToken>())
            .Returns(Failure(CreateJobError.SnapshotFailed, compensationFailed: true));

        var response = await _controller.CreateAsync(Body(), CancellationToken.None);

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(response).Value);
        Assert.Equal(true, problem.Extensions["reconciliationRequired"]);
    }

    [Fact]
    public async Task GetJob_ReturnsJob()
    {
        _jobReader.GetByIdAsync(JobId, ReadConsistency.Eventual, Arg.Any<CancellationToken>()).Returns(NewJob());

        var response = await _controller.GetAsync(JobId, ReadConsistency.Eventual, CancellationToken.None);

        var body = Assert.IsType<JobResponse>(Assert.IsType<OkObjectResult>(response).Value);
        Assert.Equal(JobId, body.JobId);
        Assert.Equal(UserId, body.UserId);
        Assert.Equal("Nightly report", body.Name);
        Assert.Equal(At, body.NextExecutionAt);
        Assert.Equal(JobStatus.Active, body.Status);
    }

    [Theory]
    [InlineData(ReadConsistency.Eventual)]
    [InlineData(ReadConsistency.Strong)]
    public async Task GetJob_PassesRequestedConsistencyToReader(ReadConsistency consistency)
    {
        _jobReader.GetByIdAsync(JobId, consistency, Arg.Any<CancellationToken>()).Returns(NewJob());

        await _controller.GetAsync(JobId, consistency, CancellationToken.None);

        await _jobReader.Received(1).GetByIdAsync(JobId, consistency, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetJob_ReturnsNotFound_WhenMissing()
    {
        _jobReader.GetByIdAsync(JobId, Arg.Any<ReadConsistency>(), Arg.Any<CancellationToken>()).Returns((Job?)null);

        var response = await _controller.GetAsync(JobId, ReadConsistency.Strong, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(response);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ProblemDetails>(notFound.Value).Status);
    }

    [Fact]
    public async Task GetJob_ReturnsNotFound_WhenSoftDeleted()
    {
        _jobReader.GetByIdAsync(JobId, Arg.Any<ReadConsistency>(), Arg.Any<CancellationToken>())
            .Returns(NewJob(deletedAt: At));

        var response = await _controller.GetAsync(JobId, ReadConsistency.Strong, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(response);
    }

    [Fact]
    public async Task GetSnapshot_ReturnsSnapshot()
    {
        _snapshotReader.GetByJobIdAsync(JobId, ReadConsistency.Strong, Arg.Any<CancellationToken>())
            .Returns(new ScheduledJobSnapshot
            {
                JobId = JobId,
                ScheduleType = ScheduleType.Cron,
                ScheduleExpression = "0 2 * * *",
                NextExecutionAt = At,
                JobVersion = 1,
                IsEnabled = true,
                Configuration = "{}",
                ConcurrencyMode = ConcurrencyMode.Singleton,
                SyncedAt = At,
            });

        var response = await _controller.GetSnapshotAsync(JobId, ReadConsistency.Strong, CancellationToken.None);

        var body = Assert.IsType<ScheduledJobSnapshotResponse>(Assert.IsType<OkObjectResult>(response).Value);
        Assert.Equal(JobId, body.JobId);
        Assert.True(body.IsEnabled);
        Assert.Equal(At, body.NextExecutionAt);
        Assert.Equal(At, body.SyncedAt);
    }

    [Fact]
    public async Task GetSnapshot_ReturnsNotFound_WhenMissing()
    {
        _snapshotReader.GetByJobIdAsync(JobId, Arg.Any<ReadConsistency>(), Arg.Any<CancellationToken>())
            .Returns((ScheduledJobSnapshot?)null);

        var response = await _controller.GetSnapshotAsync(JobId, ReadConsistency.Eventual, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(response);
    }
}
