using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Schema;

namespace Jaunty.Scaffolding.Providers.MySql;

/// <summary>
/// Maps MySQL column types to C# types.
/// </summary>
public sealed class MySqlTypeMapper : ITypeMapper
{
    /// <inheritdoc />
    public CSharpTypeInfo MapToCSharpType(ColumnSchema column)
    {
        var dataType = column.DataType.ToLowerInvariant().Trim();

        // MySQL's tinyint(1)/bit(1) boolean convention is carried by the column's display
        // width, which only appears in COLUMN_TYPE (e.g. "tinyint(1)", "bit(1) unsigned").
        // MaxLength comes from CHARACTER_MAXIMUM_LENGTH, which INFORMATION_SCHEMA always
        // reports as NULL for numeric columns, so it can never signal this convention.
        bool isSingleBitOrTinyInt = IsSingleBitOrTinyInt(column.ColumnType);

        // AUD-R26: MySQL's UNSIGNED modifier also lives only in COLUMN_TYPE ("bigint(20)
        // unsigned"), and every integer type was previously mapped to its signed C# counterpart
        // regardless. That is not a cosmetic mismatch - the upper half of each unsigned range is
        // unrepresentable, so BIGINT UNSIGNED above long.MaxValue and INT UNSIGNED above
        // int.MaxValue are ordinary values the scaffolded entity cannot hold. MySqlConnector
        // hands back byte/ushort/uint/ulong for these columns, so the generated property also
        // failed to match what the driver actually returns.
        bool isUnsigned = IsUnsigned(column.ColumnType);

        return dataType switch
        {
            // Boolean (MySQL uses tinyint(1) for bool)
            "bit" when isSingleBitOrTinyInt => new CSharpTypeInfo { TypeName = "bool", IsValueType = true },
            "bool" or "boolean" => new CSharpTypeInfo { TypeName = "bool", IsValueType = true },

            // Integer types. The tinyint(1) boolean convention is checked first - "tinyint(1)
            // unsigned" is still a flag column, not a byte.
            "tinyint" when isSingleBitOrTinyInt => new CSharpTypeInfo { TypeName = "bool", IsValueType = true },
            "tinyint" when isUnsigned => new CSharpTypeInfo { TypeName = "byte", IsValueType = true },
            "tinyint" => new CSharpTypeInfo { TypeName = "sbyte", IsValueType = true },
            "smallint" when isUnsigned => new CSharpTypeInfo { TypeName = "ushort", IsValueType = true },
            "smallint" => new CSharpTypeInfo { TypeName = "short", IsValueType = true },
            "mediumint" or "int" or "integer" when isUnsigned =>
                new CSharpTypeInfo { TypeName = "uint", IsValueType = true },
            "mediumint" or "int" or "integer" => new CSharpTypeInfo { TypeName = "int", IsValueType = true },
            "bigint" when isUnsigned => new CSharpTypeInfo { TypeName = "ulong", IsValueType = true },
            "bigint" => new CSharpTypeInfo { TypeName = "long", IsValueType = true },

            // Bit types
            "bit" => new CSharpTypeInfo { TypeName = "ulong", IsValueType = true },

            // Floating point
            "float" => new CSharpTypeInfo { TypeName = "float", IsValueType = true },
            "double" or "real" => new CSharpTypeInfo { TypeName = "double", IsValueType = true },

            // Decimal
            "decimal" or "numeric" or "dec" or "fixed" =>
                new CSharpTypeInfo { TypeName = "decimal", IsValueType = true },

            // String types
            "char" or "varchar" or "tinytext" or "text" or "mediumtext" or "longtext" =>
                new CSharpTypeInfo { TypeName = "string", IsValueType = false },

            // Binary types
            "binary" or "varbinary" or "tinyblob" or "blob" or "mediumblob" or "longblob" =>
                new CSharpTypeInfo { TypeName = "byte[]", IsValueType = false },

            // Date/Time types
            "date" => new CSharpTypeInfo { TypeName = "DateOnly", IsValueType = true, RequiredUsing = "System" },
            // AUD-R35-039: TimeSpan, not TimeOnly. MySQL's TIME is a signed duration spanning
            // -838:59:59 to 838:59:59, not a clock time, and MySqlConnector returns TimeSpan for it.
            // Every value outside [00:00:00, 24:00:00) - which is the whole reason the type has that
            // range - was unrepresentable, and the property type did not match what the driver hands
            // back either way. The mapping reads as a copy of SqlServerTypeMapper's, where TimeOnly
            // is right because SQL Server's time genuinely is a time of day.
            "time" => new CSharpTypeInfo { TypeName = "TimeSpan", IsValueType = true, RequiredUsing = "System" },
            "datetime" or "timestamp" => new CSharpTypeInfo { TypeName = "DateTime", IsValueType = true, RequiredUsing = "System" },
            // AUD-R35-040: int, not short. The 1901-2155 range fits either, but MySqlConnector
            // surfaces YEAR columns as int, and the scaffolded property has to match what the driver
            // returns - the same driver-agreement issue AUD-R26-017 fixed for the UNSIGNED family.
            "year" => new CSharpTypeInfo { TypeName = "int", IsValueType = true },

            // Enum and Set (map to string)
            "enum" or "set" => new CSharpTypeInfo { TypeName = "string", IsValueType = false },

            // JSON
            "json" => new CSharpTypeInfo { TypeName = "string", IsValueType = false },

            // Spatial types - map to byte[] for now
            "geometry" or "point" or "linestring" or "polygon" or "multipoint" or
            // AUD-R35-041: "geomcollection" as well, MySQL 8's preferred spelling for the same
            // type. INFORMATION_SCHEMA.COLUMNS.DATA_TYPE reports whichever spelling the column was
            // declared with, so one declaration scaffolded to byte[] and the other fell through to
            // the object catch-all.
            "multilinestring" or "multipolygon" or "geometrycollection" or "geomcollection" =>
                new CSharpTypeInfo { TypeName = "byte[]", IsValueType = false },

            // Default to object for unknown types
            _ => new CSharpTypeInfo { TypeName = "object", IsValueType = false }
        };
    }

    /// <summary>
    /// Detects MySQL's UNSIGNED modifier, which appears only in COLUMN_TYPE - e.g.
    /// <c>"int(10) unsigned"</c>, or <c>"bigint(20) unsigned zerofill"</c> when ZEROFILL is also
    /// set (ZEROFILL implies UNSIGNED). Matched as a whole word so a column type could not pick
    /// it up from some longer token.
    /// </summary>
    private static bool IsUnsigned(string? columnType)
    {
        if (string.IsNullOrEmpty(columnType))
            return false;

        var normalized = columnType.ToLowerInvariant();
        var index = normalized.IndexOf("unsigned", StringComparison.Ordinal);

        while (index >= 0)
        {
            var precededByBoundary = index == 0 || !char.IsLetterOrDigit(normalized[index - 1]);
            var end = index + "unsigned".Length;
            var followedByBoundary = end == normalized.Length || !char.IsLetterOrDigit(normalized[end]);

            if (precededByBoundary && followedByBoundary)
                return true;

            index = normalized.IndexOf("unsigned", index + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool IsSingleBitOrTinyInt(string? columnType)
    {
        if (string.IsNullOrEmpty(columnType))
            return false;

        var normalized = columnType.ToLowerInvariant();
        return normalized.StartsWith("tinyint(1)", StringComparison.Ordinal) ||
               normalized.StartsWith("bit(1)", StringComparison.Ordinal);
    }
}