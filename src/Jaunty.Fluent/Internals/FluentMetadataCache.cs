using System.Collections;
using System.Collections.Concurrent;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;

using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent.Internals;

internal static class FluentMetadataCache
{
    private static readonly ConcurrentDictionary<Type, EntityMetadata> _metadataCache = new();
    private static readonly ConcurrentDictionary<(Type, Type), CachedDialectMetadata> _dialectCache = new();

    public static EntityMetadata GetMetadata<T>() where T : new()
    {
        return _metadataCache.GetOrAdd(typeof(T), _ =>
        {
            if (TryBuildFromSourceGenerated(typeof(T)) is EntityMetadata sourceGenMetadata)
            {
                return sourceGenMetadata;
            }

            if (JauntyConfig.ReflectionTableMetadataResolver?.Invoke(typeof(T)) is EntityMetadata metadata)
            {
                return metadata;
            }

            throw new InvalidOperationException(
                $"No metadata found for type '{typeof(T).Name}'. " +
                "Ensure the class has [Table] and is processed by the Jaunty source generator " +
                "(the class must be declared 'partial'), or call " +
                "Jaunty.Extensions.Reflection's UseReflectionMapping().");
        });
    }

    /// <summary>
    /// Builds <see cref="EntityMetadata"/> from a source-generated entity's static
    /// <c>TableName</c>/<c>SchemaName</c>/<c>ParameterMap</c> members, with no dependency on
    /// <see cref="JauntyConfig.ReflectionTableMetadataResolver"/>. Returns <see langword="null"/>
    /// when <paramref name="type"/> has no such static surface (not source-generated), in which
    /// case the caller falls back to the reflection resolver.
    /// </summary>
#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2070", Justification = "Source-generated static members are always preserved because the generated class itself is reachable.")]
#endif
    private static EntityMetadata? TryBuildFromSourceGenerated(Type type)
    {
        PropertyInfo? tableNameProp = type.GetProperty("TableName", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo? parameterMapProp = type.GetProperty("ParameterMap", BindingFlags.Public | BindingFlags.Static);
        if (tableNameProp is not { PropertyType.Name: nameof(String) } || parameterMapProp is null)
            return null;

        if (parameterMapProp.GetValue(null) is not IDictionary parameterMap)
            return null;

        var tableName = (string)tableNameProp.GetValue(null)!;
        var schemaName = type.GetProperty("SchemaName", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;

        Type? columnInfoType = null;
        PropertyInfo? columnNameProp = null;
        PropertyInfo? propertyNameProp = null;
        PropertyInfo? isPrimaryKeyProp = null;
        PropertyInfo? isIdentityProp = null;
        PropertyInfo? propertyTypeProp = null;
        PropertyInfo? getterProp = null;
        PropertyInfo? setterProp = null;

        var columns = new List<ColumnMetadata>(parameterMap.Count);
        foreach (DictionaryEntry entry in parameterMap)
        {
            object columnInfo = entry.Value!;
            if (columnInfoType is null)
            {
                columnInfoType = columnInfo.GetType();
                columnNameProp = columnInfoType.GetProperty("ColumnName")!;
                propertyNameProp = columnInfoType.GetProperty("PropertyName")!;
                isPrimaryKeyProp = columnInfoType.GetProperty("IsPrimaryKey")!;
                isIdentityProp = columnInfoType.GetProperty("IsIdentity")!;
                propertyTypeProp = columnInfoType.GetProperty("PropertyType")!;
                getterProp = columnInfoType.GetProperty("Getter")!;
                setterProp = columnInfoType.GetProperty("Setter")!;
            }

            columns.Add(new ColumnMetadata(
                (string)propertyNameProp!.GetValue(columnInfo)!,
                (Type)propertyTypeProp!.GetValue(columnInfo)!,
                (string)columnNameProp!.GetValue(columnInfo)!,
                (bool)isPrimaryKeyProp!.GetValue(columnInfo)!,
                (bool)isIdentityProp!.GetValue(columnInfo)!,
                (Func<object, object?>)getterProp!.GetValue(columnInfo)!,
                (Action<object, object?>)setterProp!.GetValue(columnInfo)!));
        }

        return new EntityMetadata(tableName, schemaName, columns);
    }

    public static CachedDialectMetadata GetForDialect<T>(ISqlDialect dialect) where T : new()
    {
        (Type, Type) key = (typeof(T), dialect.GetType());
        return _dialectCache.GetOrAdd(key, _ =>
        {
            EntityMetadata meta = GetMetadata<T>();
            var escapedTable = dialect.EscapeTableName(meta.SchemaName, meta.TableName);
            var escapedCols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (ColumnMetadata col in meta.Columns)
            {
                escapedCols[col.PropertyName] = dialect.EscapeColumnName(col.ColumnName);
            }

            return new CachedDialectMetadata(escapedTable, escapedCols);
        });
    }
}