namespace JobScheduler.Api.Contracts;

public sealed record CreateJobResponse(Guid JobId, string CorrelationId);
