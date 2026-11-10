namespace JobScheduler.Application.Abstractions;

/// <summary>Eventual reads may be served by the geo-replica and be stale; Strong reads go to the primary.</summary>
public enum ReadConsistency
{
    Eventual,
    Strong
}
