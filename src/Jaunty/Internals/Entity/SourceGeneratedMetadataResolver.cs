using Jaunty.Interfaces;

namespace Jaunty.Internals.Entity;

/// <summary>
/// Builds <see cref="EntityMetadata"/> from a source-generated entity's
/// <see cref="IEntityMetadataSource"/> implementation, with no runtime reflection on either
/// side of the boundary. Shared by <c>CrudSqlCache</c> (core CRUD) and <c>WriteParameterCache</c>
/// (parameter binding) so the synthesis logic lives in one place.
/// </summary>
internal static class SourceGeneratedMetadataResolver
{
    /// <summary>
    /// Attempts to build <see cref="EntityMetadata"/> from <typeparamref name="T"/>'s
    /// source-generated <see cref="IEntityMetadataSource"/> implementation. Returns
    /// <see langword="null"/> when <typeparamref name="T"/> does not implement
    /// <see cref="IEntityMetadataSource"/> (not source-generated), in which case the caller
    /// should fall back to <c>JauntyConfig.ReflectionTableMetadataResolver</c>.
    /// </summary>
    public static EntityMetadata? TryBuild<T>() where T : new()
    {
        if (new T() is not IEntityMetadataSource source)
            return null;

        IReadOnlyList<EntityColumnInfo> sourceColumns = source.Columns;
        var columns = new List<ColumnMetadata>(sourceColumns.Count);
        for (int i = 0; i < sourceColumns.Count; i++)
        {
            EntityColumnInfo c = sourceColumns[i];
            columns.Add(new ColumnMetadata(c.PropertyName, c.PropertyType, c.ColumnName, c.IsPrimaryKey, c.IsIdentity, c.Getter, c.Setter));
        }

        return new EntityMetadata(source.TableName, source.SchemaName, columns);
    }
}
