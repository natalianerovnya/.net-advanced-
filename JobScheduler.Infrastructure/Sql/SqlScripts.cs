
namespace JobScheduler.Infrastructure.Sql;

/// <summary>Gives access to the SQL scripts embedded in this assembly (used by tooling and integration tests).</summary>
public static class SqlScripts
{
    public static string JobDbCreate => Read("JobDb.001_Create.sql");
    public static string JobDbSeed => Read("JobDb.Seed.sql");
    public static string ScheduleDbCreate => Read("ScheduleDb.001_Create.sql");
    public static string ScheduleDbSeed => Read("ScheduleDb.Seed.sql");

    private static string Read(string name)
    {
        var assembly = typeof(SqlScripts).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith(name, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
