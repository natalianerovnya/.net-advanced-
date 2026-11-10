using System.ComponentModel.DataAnnotations;

namespace JobScheduler.Infrastructure.Data;

public class DatabaseOptions
{
    [Required(AllowEmptyStrings = false)]
    public string Primary { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Replica { get; set; } = string.Empty;
}
