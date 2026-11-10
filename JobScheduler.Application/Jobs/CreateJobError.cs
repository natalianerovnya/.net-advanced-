namespace JobScheduler.Application.Jobs;

public enum CreateJobError
{
    None,
    ValidationFailed,
    UserNotFound,
    UserInactive,
    DuplicateName,
    SnapshotFailed
}
