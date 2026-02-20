using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Internals.Entity;

namespace Jaunty.Extensions.Reflection;

internal static class MetadataBuilder
{
    public static EntityMetadata Build<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>()
    {
        var type = typeof(T);
        string? schemaName = JauntyConfig.SchemaNameResolver?.Invoke(type);
        string tableName = JauntyConfig.TableNameResolver?.Invoke(type) ?? type.Name;

        // Attributes override resolvers
        var tableAttr = type.GetCustomAttribute<TableAttribute>();
        if (tableAttr != null)
        {
            if (tableAttr.Name != null) tableName = tableAttr.Name;
            if (tableAttr.Schema != null) schemaName = tableAttr.Schema;
        }

        var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var columns = new List<ColumnMetadata>();

        foreach (var p in props)
        {
            if (p.GetCustomAttribute<IgnoreAttribute>() != null) continue;
            if (!p.CanWrite) continue;

            var colAttr = p.GetCustomAttribute<ColumnAttribute>();
            string colName = colAttr?.Name ?? p.Name;
            bool isKey = p.GetCustomAttribute<KeyAttribute>() != null || p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase);
            
            var genAttr = p.GetCustomAttribute<DatabaseGeneratedAttribute>();
            DatabaseGeneratedOption? genOption = genAttr?.Option;

            columns.Add(new ColumnMetadata(p, colName, isKey, genOption));
        }

        return new EntityMetadata(tableName, schemaName, columns);
    }
}
