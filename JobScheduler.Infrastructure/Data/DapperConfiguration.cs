using System.Data;
using Dapper;
using JobScheduler.Domain.Enums;

namespace JobScheduler.Infrastructure.Data;

/// <summary>Registers Dapper type handlers once per process.</summary>
public static class DapperConfiguration
{
    private static readonly object Gate = new();
    private static bool _registered;

    public static void Register()
    {
        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            SqlMapper.AddTypeHandler(new EnumStringTypeHandler<ScheduleType>());
            SqlMapper.AddTypeHandler(new EnumStringTypeHandler<ConcurrencyMode>());
            SqlMapper.AddTypeHandler(new EnumStringTypeHandler<JobStatus>());
            SqlMapper.AddTypeHandler(new DateTimeOffsetUtcTypeHandler());
            _registered = true;
        }
    }
}
