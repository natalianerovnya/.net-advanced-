namespace JobScheduler.Domain;

public sealed class User
{
    public Guid UserId { get; init; }

    public required string Name { get; init; }

    public required string Email { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public bool IsActive { get; init; }
}
