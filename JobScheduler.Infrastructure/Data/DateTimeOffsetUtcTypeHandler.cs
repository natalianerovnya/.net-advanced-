using System.Data;
using Dapper;

namespace JobScheduler.Infrastructure.Data;

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
