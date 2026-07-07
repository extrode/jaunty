using System.Collections;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;

namespace Jaunty.Internals.Entity;

/// <summary>
/// Builds <see cref="EntityMetadata"/> from a source-generated entity's static
/// <c>TableName</c>/<c>SchemaName</c>/<c>ParameterMap</c> members, with no dependency on
/// runtime reflection resolvers. Shared by <c>CrudSqlCache</c> (core CRUD) and
/// <c>FluentMetadataCache</c> (fluent queries) so the synthesis logic lives in one place.
/// </summary>
internal static class SourceGeneratedMetadataResolver
{
    /// <summary>
    /// Attempts to build <see cref="EntityMetadata"/> from <paramref name="type"/>'s
    /// source-generated static surface. Returns <see langword="null"/> when <paramref name="type"/>
    /// has no such static surface (not source-generated), in which case the caller should fall
    /// back to <c>JauntyConfig.ReflectionTableMetadataResolver</c>.
    /// </summary>
#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2070", Justification = "Source-generated static members are always preserved because the generated class itself is reachable.")]
    [UnconditionalSuppressMessage("AOT", "IL2075", Justification = "The ColumnInfo nested type's properties are always preserved because the generated class itself is reachable.")]
#endif
    public static EntityMetadata? TryBuild(Type type)
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
}
