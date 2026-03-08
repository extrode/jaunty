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

            // String types
            "char" or "character" or "varchar" or "character varying" or "text" or "name" =>
                new CSharpTypeInfo { TypeName = "string", IsValueType = false },

            // Date/Time types
            "date" => new CSharpTypeInfo { TypeName = "DateOnly", IsValueType = true },
            "time" or "time without time zone" => new CSharpTypeInfo { TypeName = "TimeOnly", IsValueType = true },
            "time with time zone" or "timetz" => new CSharpTypeInfo { TypeName = "TimeOnly", IsValueType = true },
            "timestamp" or "timestamp without time zone" => new CSharpTypeInfo { TypeName = "DateTime", IsValueType = true },
            "timestamp with time zone" or "timestamptz" => new CSharpTypeInfo { TypeName = "DateTimeOffset", IsValueType = true },
            "interval" => new CSharpTypeInfo { TypeName = "TimeSpan", IsValueType = true },

            // UUID
            "uuid" => new CSharpTypeInfo { TypeName = "Guid", IsValueType = true },

            // Binary
            "bytea" => new CSharpTypeInfo { TypeName = "byte[]", IsValueType = false },

            // JSON
            "json" or "jsonb" => new CSharpTypeInfo { TypeName = "string", IsValueType = false },

            // XML
            "xml" => new CSharpTypeInfo { TypeName = "string", IsValueType = false },

            // Network types
            "inet" or "cidr" => new CSharpTypeInfo { TypeName = "string", IsValueType = false },
            "macaddr" or "macaddr8" => new CSharpTypeInfo { TypeName = "string", IsValueType = false },

            // Bit strings
            "bit" or "bit varying" or "varbit" => new CSharpTypeInfo { TypeName = "bool", IsValueType = true },

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