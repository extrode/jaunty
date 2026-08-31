using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Schema;

namespace Jaunty.Scaffolding.Providers.SQLite;

/// <summary>
/// Maps SQLite column types to C# types.
/// </summary>
public sealed class SQLiteTypeMapper : ITypeMapper
{
    /// <inheritdoc />
    public CSharpTypeInfo MapToCSharpType(ColumnSchema column)
    {
        // SQLite uses type affinity - normalize the declared type
        var dataType = column.DataType.ToUpperInvariant().Trim();

        // Extract base type (remove size specifications)
        var parenIndex = dataType.IndexOf('(');
        if (parenIndex > 0)
            dataType = dataType[..parenIndex].Trim();

        return dataType switch
        {
            // Integer types - SQLite stores all integers as 64-bit, so every spelling maps to long
            // regardless of the width the declaration names. AUD-R35-270: BIGINT used to sit in an
            // arm of its own under the comment "Explicitly use int for common names", which
            // described neither the arm it labelled (it returns long) nor any decision this switch
            // makes.
            "INT" or "INTEGER" or "TINYINT" or "SMALLINT" or "MEDIUMINT" or "INT2" or "INT8" or "BIGINT" =>
                new CSharpTypeInfo { TypeName = "long", IsValueType = true },

            // Real types
            "REAL" or "DOUBLE" or "DOUBLE PRECISION" or "FLOAT" =>
                new CSharpTypeInfo { TypeName = "double", IsValueType = true },

            // Numeric/Decimal
            "NUMERIC" or "DECIMAL" =>
                new CSharpTypeInfo { TypeName = "decimal", IsValueType = true },

            // Text types
            "TEXT" or "CHAR" or "CHARACTER" or "VARCHAR" or "VARYING CHARACTER" or
            "NCHAR" or "NATIVE CHARACTER" or "NVARCHAR" or "CLOB" =>
                new CSharpTypeInfo { TypeName = "string", IsValueType = false },

            // Blob
            "BLOB" or "NONE" =>
                new CSharpTypeInfo { TypeName = "byte[]", IsValueType = false },

            // Boolean (SQLite stores as 0/1)
            "BOOLEAN" or "BOOL" =>
                new CSharpTypeInfo { TypeName = "bool", IsValueType = true },

            // Date/Time (SQLite stores as text or numbers)
            "DATE" =>
                new CSharpTypeInfo { TypeName = "DateOnly", IsValueType = true, RequiredUsing = "System" },

            "TIME" =>
                new CSharpTypeInfo { TypeName = "TimeOnly", IsValueType = true, RequiredUsing = "System" },

            "DATETIME" or "TIMESTAMP" =>
                new CSharpTypeInfo { TypeName = "DateTime", IsValueType = true, RequiredUsing = "System" },

            // GUID/UUID (SQLite stores as text or blob)
            "GUID" or "UUID" or "UNIQUEIDENTIFIER" =>
                new CSharpTypeInfo { TypeName = "Guid", IsValueType = true, RequiredUsing = "System" },

            // Default to object for unknown types
            _ => new CSharpTypeInfo { TypeName = "object", IsValueType = false }
        };
    }
}