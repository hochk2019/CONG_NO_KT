using System.Data;
using Dapper;

namespace CongNoGolden.Infrastructure.Data;

public static class DapperTypeHandlers
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(new DateOnlyHandler());
        SqlMapper.AddTypeHandler(new NullableDateOnlyHandler());
        _registered = true;
    }

    private sealed class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.ToDateTime(TimeOnly.MinValue);
        }

        public override DateOnly Parse(object value)
        {
            if (value is DateTime dateTime)
            {
                return DateOnly.FromDateTime(dateTime);
            }

            if (value is DateOnly dateOnly)
            {
                return dateOnly;
            }

            if (value is string text && DateOnly.TryParse(text, out var parsed))
            {
                return parsed;
            }

            throw new DataException($"Cannot convert {value.GetType()} to DateOnly.");
        }
    }

    private sealed class NullableDateOnlyHandler : SqlMapper.TypeHandler<DateOnly?>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly? value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.HasValue
                ? value.Value.ToDateTime(TimeOnly.MinValue)
                : DBNull.Value;
        }

        public override DateOnly? Parse(object value)
        {
            if (value is null || value is DBNull)
            {
                return null;
            }

            if (value is DateTime dateTime)
            {
                return DateOnly.FromDateTime(dateTime);
            }

            if (value is DateOnly dateOnly)
            {
                return dateOnly;
            }

            if (value is string text && DateOnly.TryParse(text, out var parsed))
            {
                return parsed;
            }

            throw new DataException($"Cannot convert {value.GetType()} to DateOnly?.");
        }
    }
}
