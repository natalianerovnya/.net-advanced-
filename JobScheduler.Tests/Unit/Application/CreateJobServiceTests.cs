using JobScheduler.Application.Abstractions;
using JobScheduler.Application.Jobs;
using JobScheduler.Domain;
using JobScheduler.Domain.Enums;
using JobScheduler.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace JobScheduler.Tests.Unit.Application;

public class CreateJobServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly IUserReader _users = Substitute.For<IUserReader>();
    private readonly IJobReader _jobReader = Substitute.For<IJobReader>();
    private readonly IJobWriter _jobWriter = Substitute.For<IJobWriter>();
    private readonly IScheduledJobSnapshotWriter _snapshotWriter = Substitute.For<IScheduledJobSnapshotWriter>();
    private readonly IReconciliationFlagger _reconciliation = Substitute.For<IReconciliationFlagger>();
    private readonly CreateJobService _service;

    public CreateJobServiceTests()
    {
        _users.GetByIdAsync(UserId, Arg.Any<ReadConsistency>(), Arg.Any<CancellationToken>())
            .Returns(new User { UserId = UserId, Name = "Alice", Email = "alice@example.com", IsActive = true });
        _jobReader.ExistsByNameAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<ReadConsistency>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _service = new CreateJobService(
            _users,
            _jobReader,
            _jobWriter,
            _snapshotWriter,
            _reconciliation,
            new FixedTimeProvider(Now),
            NullLogger<CreateJobService>.Instance);
    }

    private static CreateJobRequest ValidRequest(Action<CreateJobRequestBuilder>? customize = null)
    {
        var builder = new CreateJobRequestBuilder();
        customize?.Invoke(builder);
        return builder.Build();
    }

    [Fact]
    public async Task Create_HappyPath_WritesJobThenSnapshot()
    {
        Job? job = null;
        ScheduledJobSnapshot? snapshot = null;
        await _jobWriter.AddAsync(Arg.Do<Job>(j => job = j), Arg.Any<CancellationToken>());
        await _snapshotWriter.AddAsync(Arg.Do<ScheduledJobSnapshot>(s => snapshot = s), Arg.Any<CancellationToken>());

        var result = await _service.CreateAsync(ValidRequest(r => r.Name = "  Nightly report  "));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.JobId);
        Assert.False(string.IsNullOrWhiteSpace(result.CorrelationId));
        Assert.NotNull(job);
        Assert.NotNull(snapshot);
        Assert.Equal(result.JobId, job.JobId);
        Assert.Equal("Nightly report", job.Name);
        Assert.Equal(JobStatus.Active, job.Status);
        Assert.Equal(1, job.JobVersion);
        Assert.Equal(Now, job.CreatedAt);
        Assert.Equal(new DateTimeOffset(2026, 11, 11, 2, 0, 0, TimeSpan.Zero), job.NextExecutionAtUtc);
        Assert.Equal(job.JobId, snapshot.JobId);
        Assert.Equal(job.NextExecutionAtUtc, snapshot.NextExecutionAt);
        Assert.Equal(job.JobVersion, snapshot.JobVersion);
        Assert.Equal(job.Configuration, snapshot.Configuration);
        Assert.True(snapshot.IsEnabled);

        Received.InOrder(() =>
        {
            _jobWriter.AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
            _snapshotWriter.AddAsync(Arg.Any<ScheduledJobSnapshot>(), Arg.Any<CancellationToken>());
        });
        await _jobWriter.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_UsesCallerCorrelationId_WhenProvided()
    {
        var result = await _service.CreateAsync(ValidRequest(r => r.CorrelationId = "corr-123"));

        Assert.Equal("corr-123", result.CorrelationId);
    }

    [Fact]
    public async Task Create_ChecksUserEventually_AndDuplicatesStrongly()
    {
        await _service.CreateAsync(ValidRequest());

        await _users.Received(1).GetByIdAsync(UserId, ReadConsistency.Eventual, Arg.Any<CancellationToken>());
        await _jobReader.Received(1).ExistsByNameAsync(UserId, "Nightly report", ReadConsistency.Strong, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_OneTimeJob_UsesTheGivenTimeAsFirstExecution()
    {
        var result = await _service.CreateAsync(ValidRequest(r =>
        {
            r.ScheduleType = ScheduleType.OneTime;
            r.ScheduleExpression = "2027-01-01T00:00:00Z";
        }));

        Assert.True(result.Succeeded);
        await _jobWriter.Received(1).AddAsync(
            Arg.Is<Job>(j => j.NextExecutionAtUtc == new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_RejectsBlankName(string name)
    {
        var result = await _service.CreateAsync(ValidRequest(r => r.Name = name));

        await AssertRejectedWithoutWrites(result, CreateJobError.ValidationFailed);
    }

    [Fact]
    public async Task Create_RejectsTooLongName()
    {
        var result = await _service.CreateAsync(ValidRequest(r => r.Name = new string('a', 201)));

        await AssertRejectedWithoutWrites(result, CreateJobError.ValidationFailed);
    }

    [Fact]
    public async Task Create_RejectsEmptyUserId()
    {
        var result = await _service.CreateAsync(ValidRequest(r => r.UserId = Guid.Empty));

        await AssertRejectedWithoutWrites(result, CreateJobError.ValidationFailed);
    }

    [Theory]
    [InlineData("not a cron")]
    [InlineData("* * *")]
    [InlineData("")]
    public async Task Create_RejectsInvalidCronExpression(string expression)
    {
        var result = await _service.CreateAsync(ValidRequest(r => r.ScheduleExpression = expression));

        await AssertRejectedWithoutWrites(result, CreateJobError.ValidationFailed);
    }

    [Theory]
    [InlineData("2026-01-01T00:00:00Z")]
    [InlineData("tomorrow-ish")]
    public async Task Create_RejectsOneTimeJobWithPastOrInvalidTime(string expression)
    {
        var result = await _service.CreateAsync(ValidRequest(r =>
        {
            r.ScheduleType = ScheduleType.OneTime;
            r.ScheduleExpression = expression;
        }));

        await AssertRejectedWithoutWrites(result, CreateJobError.ValidationFailed);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    public async Task Create_RejectsInvalidConfigurationJson(string configuration)
    {
        var result = await _service.CreateAsync(ValidRequest(r => r.Configuration = configuration));

        await AssertRejectedWithoutWrites(result, CreateJobError.ValidationFailed);
    }

    [Fact]
    public async Task Create_RejectsUnknownEnumValues()
    {
        var result = await _service.CreateAsync(ValidRequest(r =>
        {
            r.ScheduleType = (ScheduleType)99;
            r.ConcurrencyMode = (ConcurrencyMode)99;
        }));

        await AssertRejectedWithoutWrites(result, CreateJobError.ValidationFailed);
    }

    [Fact]
    public async Task Create_ReportsEveryValidationError()
    {
        var result = await _service.CreateAsync(ValidRequest(r =>
        {
            r.Name = " ";
            r.UserId = Guid.Empty;
        }));

        Assert.Equal(CreateJobError.ValidationFailed, result.Error);
        Assert.True(result.Messages.Count >= 2);
    }

    [Fact]
    public async Task Create_FailsWhenUserDoesNotExist()
    {
        _users.GetByIdAsync(UserId, Arg.Any<ReadConsistency>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await _service.CreateAsync(ValidRequest());

        await AssertRejectedWithoutWrites(result, CreateJobError.UserNotFound);
    }

    [Fact]
    public async Task Create_FailsWhenUserIsInactive()
    {
        _users.GetByIdAsync(UserId, Arg.Any<ReadConsistency>(), Arg.Any<CancellationToken>())
            .Returns(new User { UserId = UserId, Name = "Carol", Email = "carol@example.com", IsActive = false });

        var result = await _service.CreateAsync(ValidRequest());

        await AssertRejectedWithoutWrites(result, CreateJobError.UserInactive);
    }

    [Fact]
    public async Task Create_FailsWhenNameAlreadyExists()
    {
        _jobReader.ExistsByNameAsync(UserId, "Nightly report", ReadConsistency.Strong, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _service.CreateAsync(ValidRequest());

        await AssertRejectedWithoutWrites(result, CreateJobError.DuplicateName);
    }

    [Fact]
    public async Task Create_MapsUniqueIndexViolationToDuplicateName_WithoutTouchingSnapshot()
    {
        _jobWriter.AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DuplicateJobNameException(UserId, "Nightly report"));

        var result = await _service.CreateAsync(ValidRequest());

        Assert.Equal(CreateJobError.DuplicateName, result.Error);
        await _snapshotWriter.DidNotReceive().AddAsync(Arg.Any<ScheduledJobSnapshot>(), Arg.Any<CancellationToken>());
        await _jobWriter.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WhenSnapshotFails_DeletesJobAndReportsFailure()
    {
        Guid? createdJobId = null;
        await _jobWriter.AddAsync(Arg.Do<Job>(j => createdJobId = j.JobId), Arg.Any<CancellationToken>());
        _snapshotWriter.AddAsync(Arg.Any<ScheduledJobSnapshot>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("snapshot store down"));

        var result = await _service.CreateAsync(ValidRequest(r => r.CorrelationId = "corr-1"));

        Assert.Equal(CreateJobError.SnapshotFailed, result.Error);
        Assert.Null(result.JobId);
        Assert.False(result.CompensationFailed);
        Assert.Equal("corr-1", result.CorrelationId);
        await _jobWriter.Received(1).DeleteAsync(createdJobId!.Value, Arg.Any<CancellationToken>());
        await _reconciliation.DidNotReceive().FlagAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WhenSnapshotAndCompensationFail_FlagsJobForReconciliation()
    {
        Guid? createdJobId = null;
        await _jobWriter.AddAsync(Arg.Do<Job>(j => createdJobId = j.JobId), Arg.Any<CancellationToken>());
        _snapshotWriter.AddAsync(Arg.Any<ScheduledJobSnapshot>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("snapshot store down"));
        _jobWriter.DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("job store down"));

        var result = await _service.CreateAsync(ValidRequest(r => r.CorrelationId = "corr-2"));

        Assert.Equal(CreateJobError.SnapshotFailed, result.Error);
        Assert.True(result.CompensationFailed);
        await _reconciliation.Received(1).FlagAsync(
            createdJobId!.Value, "corr-2", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WhenReconciliationFlaggingAlsoFails_StillReturnsFailureResult()
    {
        _snapshotWriter.AddAsync(Arg.Any<ScheduledJobSnapshot>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("snapshot store down"));
        _jobWriter.DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("job store down"));
        _reconciliation.FlagAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("flag store down"));

        var result = await _service.CreateAsync(ValidRequest());

        Assert.Equal(CreateJobError.SnapshotFailed, result.Error);
        Assert.True(result.CompensationFailed);
    }

    [Fact]
    public async Task Create_RunsCompensationEvenWhenCallerCancelled()
    {
        using var cts = new CancellationTokenSource();
        _snapshotWriter.AddAsync(Arg.Any<ScheduledJobSnapshot>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cts.Cancel();
                return Task.FromException(new OperationCanceledException(cts.Token));
            });

        var result = await _service.CreateAsync(ValidRequest(), cts.Token);

        Assert.Equal(CreateJobError.SnapshotFailed, result.Error);
        await _jobWriter.Received(1).DeleteAsync(Arg.Any<Guid>(), CancellationToken.None);
    }

    [Fact]
    public async Task Create_ThrowsForNullRequest()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.CreateAsync(null!));
    }

    private async Task AssertRejectedWithoutWrites(CreateJobResult result, CreateJobError expected)
    {
        Assert.False(result.Succeeded);
        Assert.Equal(expected, result.Error);
        Assert.Null(result.JobId);
        Assert.NotEmpty(result.Messages);
        await _jobWriter.DidNotReceive().AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
        await _snapshotWriter.DidNotReceive().AddAsync(Arg.Any<ScheduledJobSnapshot>(), Arg.Any<CancellationToken>());
    }
}
