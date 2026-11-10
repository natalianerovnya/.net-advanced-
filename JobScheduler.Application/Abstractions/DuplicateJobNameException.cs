namespace JobScheduler.Application.Abstractions;

public sealed class DuplicateJobNameException : Exception
{
    public DuplicateJobNameException(Guid userId, string name, Exception? inner = null)
        : base($"User '{userId}' already has a job named '{name}'.", inner)
    {
        UserId = userId;
        JobName = name;
    }

    public Guid UserId { get; }

    public string JobName { get; }
}
