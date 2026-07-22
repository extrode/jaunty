using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Schema;

namespace Jaunty.Scaffolding.Providers.SqlServer;

/// <summary>
/// Maps SQL Server column types to C# types.
/// </summary>
public sealed class SqlServerTypeMapper : ITypeMapper
{
    /// <inheritdoc />
    public CSharpTypeInfo MapToCSharpType(ColumnSchema column)
    {
        var dataType = column.DataType.ToLowerInvariant().Trim();

        return dataType switch
        {
            // Boolean
            "bit" => new CSharpTypeInfo { TypeName = "bool", IsValueType = true },

            // Integer types
            "tinyint" => new CSharpTypeInfo { TypeName = "byte", IsValueType = true },
            "smallint" => new CSharpTypeInfo { TypeName = "short", IsValueType = true },
            "int" => new CSharpTypeInfo { TypeName = "int", IsValueType = true },
            "bigint" => new CSharpTypeInfo { TypeName = "long", IsValueType = true },

            // Floating point
            "real" => new CSharpTypeInfo { TypeName = "float", IsValueType = true },
            "float" => new CSharpTypeInfo { TypeName = "double", IsValueType = true },

            // Decimal/Money
            "decimal" or "numeric" or "money" or "smallmoney" =>
                new CSharpTypeInfo { TypeName = "decimal", IsValueType = true },

            // String types
            "char" or "varchar" or "text" or "nchar" or "nvarchar" or "ntext" or "xml" =>
                new CSharpTypeInfo { TypeName = "string", IsValueType = false },

            // Date/Time types
            "date" => new CSharpTypeInfo { TypeName = "DateOnly", IsValueType = true, RequiredUsing = "System" },
            "time" => new CSharpTypeInfo { TypeName = "TimeOnly", IsValueType = true, RequiredUsing = "System" },
            "datetime" or "datetime2" or "smalldatetime" =>
                new CSharpTypeInfo { TypeName = "DateTime", IsValueType = true, RequiredUsing = "System" },
            "datetimeoffset" =>
                new CSharpTypeInfo { TypeName = "DateTimeOffset", IsValueType = true, RequiredUsing = "System" },

            // GUID
            "uniqueidentifier" => new CSharpTypeInfo { TypeName = "Guid", IsValueType = true, RequiredUsing = "System" },

            // Binary types
            "binary" or "varbinary" or "image" or "rowversion" or "timestamp" =>
                new CSharpTypeInfo { TypeName = "byte[]", IsValueType = false },

            // Spatial types - map to byte[] for now
            "geography" or "geometry" or "hierarchyid" =>
                new CSharpTypeInfo { TypeName = "byte[]", IsValueType = false },

            // sql_variant
            "sql_variant" => new CSharpTypeInfo { TypeName = "object", IsValueType = false },

            // Default to object for unknown types
            _ => new CSharpTypeInfo { TypeName = "object", IsValueType = false }
        };
    }
}