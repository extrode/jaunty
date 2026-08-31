using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Schema;

namespace Jaunty.Scaffolding.Providers.PostgreSql;

/// <summary>
/// Maps PostgreSQL column types to C# types.
/// </summary>
public sealed class PostgreSqlTypeMapper : ITypeMapper
{
    /// <inheritdoc />
    public CSharpTypeInfo MapToCSharpType(ColumnSchema column)
    {
        var dataType = column.DataType.ToLowerInvariant().Trim();

        return dataType switch
        {
            // Boolean
            "boolean" or "bool" => new CSharpTypeInfo { TypeName = "bool", IsValueType = true },

            // Integer types
            "smallint" or "int2" => new CSharpTypeInfo { TypeName = "short", IsValueType = true },
            "integer" or "int" or "int4" => new CSharpTypeInfo { TypeName = "int", IsValueType = true },
            "bigint" or "int8" => new CSharpTypeInfo { TypeName = "long", IsValueType = true },
            "serial" or "serial4" => new CSharpTypeInfo { TypeName = "int", IsValueType = true },
            "bigserial" or "serial8" => new CSharpTypeInfo { TypeName = "long", IsValueType = true },
            "smallserial" or "serial2" => new CSharpTypeInfo { TypeName = "short", IsValueType = true },

            // Floating point
            "real" or "float4" => new CSharpTypeInfo { TypeName = "float", IsValueType = true },
            "double precision" or "float8" => new CSharpTypeInfo { TypeName = "double", IsValueType = true },

            // Decimal/Money
            "numeric" or "decimal" => new CSharpTypeInfo { TypeName = "decimal", IsValueType = true },
            "money" => new CSharpTypeInfo { TypeName = "decimal", IsValueType = true },

            // String types.
            // The reader supplies udt_name (not the multi-word information_schema.data_type
            // spelling), so "bpchar" is the real wire value for fixed-length CHAR(n)/CHARACTER(n)
            // columns - without it those columns fall through to the "object" catch-all below.
            "char" or "character" or "bpchar" or "varchar" or "character varying" or "text" or "name" =>
                new CSharpTypeInfo { TypeName = "string", IsValueType = false },

            // Date/Time types
            "date" => new CSharpTypeInfo { TypeName = "DateOnly", IsValueType = true, RequiredUsing = "System" },
            "time" or "time without time zone" => new CSharpTypeInfo { TypeName = "TimeOnly", IsValueType = true, RequiredUsing = "System" },
            // AUD-R35-037: DateTimeOffset, not TimeOnly. timetz carries a UTC offset that TimeOnly
            // cannot hold, and Npgsql's default CLR type for it is DateTimeOffset - so the scaffolded
            // property did not merely lose the zone, it failed to materialise. The unzoned "time"
            // arm above is correct as it stands.
            "time with time zone" or "timetz" => new CSharpTypeInfo { TypeName = "DateTimeOffset", IsValueType = true, RequiredUsing = "System" },
            "timestamp" or "timestamp without time zone" => new CSharpTypeInfo { TypeName = "DateTime", IsValueType = true, RequiredUsing = "System" },
            "timestamp with time zone" or "timestamptz" => new CSharpTypeInfo { TypeName = "DateTimeOffset", IsValueType = true, RequiredUsing = "System" },
            "interval" => new CSharpTypeInfo { TypeName = "TimeSpan", IsValueType = true, RequiredUsing = "System" },

            // UUID
            "uuid" => new CSharpTypeInfo { TypeName = "Guid", IsValueType = true, RequiredUsing = "System" },

            // Binary
            "bytea" => new CSharpTypeInfo { TypeName = "byte[]", IsValueType = false },

            // JSON
            "json" or "jsonb" => new CSharpTypeInfo { TypeName = "string", IsValueType = false },

            // XML
            "xml" => new CSharpTypeInfo { TypeName = "string", IsValueType = false },

            // Network types.
            //
            // AUD-R35-038: these mapped to string, which Npgsql does not return for any of them, so
            // the scaffolded property threw on read rather than being merely imprecise. inet comes
            // back as IPAddress and macaddr/macaddr8 as PhysicalAddress, both BCL types needing only
            // a using. cidr is deliberately left alone: Npgsql returns NpgsqlCidr, a driver type, and
            // emitting it would make every scaffolded entity depend on the Npgsql package - a call
            // for the caller, not for this mapper. Same shape as AUD-R6-026's bit(1) fix, which
            // corrected only the width-1 arm.
            "inet" => new CSharpTypeInfo { TypeName = "IPAddress", IsValueType = false, RequiredUsing = "System.Net" },
            "cidr" => new CSharpTypeInfo { TypeName = "string", IsValueType = false },
            "macaddr" or "macaddr8" => new CSharpTypeInfo { TypeName = "PhysicalAddress", IsValueType = false, RequiredUsing = "System.Net.NetworkInformation" },

            // Bit strings - a single-bit bit(1)/bit varying(1) column follows the common
            // boolean-flag convention; wider bit strings hold more than one bit of data and
            // must not be collapsed to bool (mirrors MySqlTypeMapper's bit(1)/tinyint(1) handling)
            "bit" or "bit varying" or "varbit" when column.MaxLength == 1 =>
                new CSharpTypeInfo { TypeName = "bool", IsValueType = true },
            // AUD-R35-038: BitArray, not ulong. Npgsql returns BitArray for any bit string wider
            // than one, and a ulong cannot hold a bit(n) for n > 64 at all.
            "bit" or "bit varying" or "varbit" => new CSharpTypeInfo { TypeName = "BitArray", IsValueType = false, RequiredUsing = "System.Collections" },

            // Geometric types - map to string for now
            "point" or "line" or "lseg" or "box" or "path" or "polygon" or "circle" =>
                new CSharpTypeInfo { TypeName = "string", IsValueType = false },

            // OID types
            "oid" => new CSharpTypeInfo { TypeName = "uint", IsValueType = true },

            // Default to object for unknown types (including arrays)
            _ => new CSharpTypeInfo { TypeName = "object", IsValueType = false }
        };
    }
}