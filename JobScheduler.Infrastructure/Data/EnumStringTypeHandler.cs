using System.Data;
using Dapper;

namespace JobScheduler.Infrastructure.Data;

/// <summary>Stores enums as their names (nvarchar), matching the CHECK constraints.</summary>
public sealed class EnumStringTypeHandler<T> : SqlMapper.TypeHandler<T>
    where T : struct, Enum
{
    public override void SetValue(IDbDataParameter parameter, T value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString();
    }

    public override T Parse(object value) => Enum.Parse<T>((string)value, ignoreCase: false);
}
