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
            if (_registered) return;
            SqlMapper.AddTypeHandler(new EnumStringTypeHandler<ScheduleType>());
            SqlMapper.AddTypeHandler(new EnumStringTypeHandler<ConcurrencyMode>());
            SqlMapper.AddTypeHandler(new EnumStringTypeHandler<JobStatus>());
            SqlMapper.AddTypeHandler(new DateTimeOffsetUtcTypeHandler());
            _registered = true;
        }
    }
}

/// <summary>Stores enums as their names (nvarchar), matching the CHECK constraints.</summary>
public sealed class EnumStringTypeHandler<T> : SqlMapper.TypeHandler<T> where T : struct, Enum
{
    public override void SetValue(IDbDataParameter parameter, T value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString();
    }

    public override T Parse(object value) => Enum.Parse<T>((string)value, ignoreCase: false);
}

/// <summary>Always writes datetimeoffset and returns values normalized to UTC.</summary>
public sealed class DateTimeOffsetUtcTypeHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
    {
        parameter.DbType = DbType.DateTimeOffset;
        parameter.Value = value;
    }

    public override DateTimeOffset Parse(object value) => value switch
    {
        DateTimeOffset dto => dto.ToUniversalTime(),
        DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
        _ => throw new InvalidCastException($"Cannot convert {value.GetType()} to DateTimeOffset.")
    };
}
