using Jaunty.Scaffolding.Schema;

namespace Jaunty.Scaffolding.Providers;

/// <summary>
/// Shared post-processing every schema reader applies to the raw rows it read.
/// </summary>
/// <remarks>
/// AUD-R35-043: the primary-key marking loop existed three times - inline in
/// <c>MySqlSchemaReader</c> and <c>PostgreSqlSchemaReader</c>, and as
/// <c>SqlServerSchemaReader.MarkPrimaryKeyColumns</c> - each with its own hand-written
/// <see cref="ColumnSchema"/> clone, because the type is init-only and there is no
/// <c>with</c> on a class. The three copies had already diverged: MySQL's carried
/// <see cref="ColumnSchema.ColumnType"/>, the other two silently dropped it. That is latent
/// rather than live today only because MySQL is the one reader that populates
/// <c>ColumnType</c> and the one mapper that reads it - the moment another provider starts
/// reporting a raw type, every primary-key column loses it and no test anywhere fails.
/// One clone, one caller per reader, so the next property added to <c>ColumnSchema</c>
/// cannot go missing from two thirds of the readers.
/// </remarks>
internal static class SchemaReaderHelpers
{
    /// <summary>
    /// Sets <see cref="ColumnSchema.IsPrimaryKey"/> on every column named by
    /// <paramref name="primaryKey"/>, replacing the entry in <paramref name="columns"/>.
    /// </summary>
    /// <param name="columns">The table's columns, modified in place.</param>
    /// <param name="primaryKey">The primary key, or null when the table has none.</param>
    internal static void MarkPrimaryKeyColumns(List<ColumnSchema> columns, PrimaryKeyInfo? primaryKey)
    {
        if (primaryKey is null)
            return;

        for (int index = 0; index < columns.Count; index++)
        {
            ColumnSchema column = columns[index];

            if (primaryKey.Columns.Contains(column.ColumnName, StringComparer.OrdinalIgnoreCase))
                columns[index] = WithPrimaryKey(column);
        }
    }

    /// <summary>
    /// Clones <paramref name="column"/> with <see cref="ColumnSchema.IsPrimaryKey"/> set.
    /// Every other property is carried across unchanged.
    /// </summary>
    internal static ColumnSchema WithPrimaryKey(ColumnSchema column) =>
        new()
        {
            ColumnName = column.ColumnName,
            DataType = column.DataType,
            IsNullable = column.IsNullable,
            IsPrimaryKey = true,
            IsIdentity = column.IsIdentity,
            IsComputed = column.IsComputed,
            MaxLength = column.MaxLength,
            Precision = column.Precision,
            Scale = column.Scale,
            DefaultValue = column.DefaultValue,
            OrdinalPosition = column.OrdinalPosition,
            ColumnType = column.ColumnType
        };
}
