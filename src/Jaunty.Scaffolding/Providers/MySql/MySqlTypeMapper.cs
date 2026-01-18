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

        return dataType switch
        {
            // Boolean (MySQL uses tinyint(1) for bool)
            "bit" when column.MaxLength == 1 => new CSharpTypeInfo { TypeName = "bool", IsValueType = true },
            "bool" or "boolean" => new CSharpTypeInfo { TypeName = "bool", IsValueType = true },

            // Integer types
            "tinyint" when column.MaxLength == 1 => new CSharpTypeInfo { TypeName = "bool", IsValueType = true },
            "tinyint" => new CSharpTypeInfo { TypeName = "sbyte", IsValueType = true },
            "smallint" => new CSharpTypeInfo { TypeName = "short", IsValueType = true },
            "mediumint" or "int" or "integer" => new CSharpTypeInfo { TypeName = "int", IsValueType = true },
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
            "date" => new CSharpTypeInfo { TypeName = "DateOnly", IsValueType = true },
            "time" => new CSharpTypeInfo { TypeName = "TimeOnly", IsValueType = true },
            "datetime" or "timestamp" => new CSharpTypeInfo { TypeName = "DateTime", IsValueType = true },
            "year" => new CSharpTypeInfo { TypeName = "short", IsValueType = true },

            // Enum and Set (map to string)
            "enum" or "set" => new CSharpTypeInfo { TypeName = "string", IsValueType = false },

            // JSON
            "json" => new CSharpTypeInfo { TypeName = "string", IsValueType = false },

            // Spatial types - map to byte[] for now
            "geometry" or "point" or "linestring" or "polygon" or "multipoint" or
            "multilinestring" or "multipolygon" or "geometrycollection" =>
                new CSharpTypeInfo { TypeName = "byte[]", IsValueType = false },

            // Default to object for unknown types
            _ => new CSharpTypeInfo { TypeName = "object", IsValueType = false }
        };
    }
}
