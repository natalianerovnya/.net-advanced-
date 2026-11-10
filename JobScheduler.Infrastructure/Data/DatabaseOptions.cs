using System.ComponentModel.DataAnnotations;

namespace JobScheduler.Infrastructure.Data;

public class DatabaseOptions
{
    [Required(AllowEmptyStrings = false)]
    public string Primary { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string Replica { get; set; } = "";
}

public sealed class JobDbOptions : DatabaseOptions
{
    public const string SectionName = "JobScheduler:JobDb";
}

public sealed class ScheduleDbOptions : DatabaseOptions
{
    public const string SectionName = "JobScheduler:ScheduleDb";
}
