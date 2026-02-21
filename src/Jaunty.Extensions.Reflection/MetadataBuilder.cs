using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Internals.Entity;

namespace Jaunty.Extensions.Reflection;

public static class MetadataBuilder
{
    public static EntityMetadata Build<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] 
#endif
        T>()
    {
        var type = typeof(T);
        string? schemaName = JauntyConfig.SchemaNameResolver?.Invoke(type);
        string tableName = JauntyConfig.TableNameResolver?.Invoke(type) ?? type.Name;

        // 1. Table Attribute resolution (Both namespaces)
        var tableAttr = type.GetCustomAttribute<TableAttribute>();
        if (tableAttr != null)
        {
            if (tableAttr.Name != null) tableName = tableAttr.Name;
            if (tableAttr.Schema != null) schemaName = tableAttr.Schema;
        }
        else
        {
            var dataTableAttr = type.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.TableAttribute>();
            if (dataTableAttr != null)
            {
                tableName = dataTableAttr.Name;
                if (!string.IsNullOrEmpty(dataTableAttr.Schema)) schemaName = dataTableAttr.Schema;
            }
        }

        var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var columns = new List<ColumnMetadata>();

        foreach (var p in props)
        {
            // 2. Ignore resolution
            if (p.GetCustomAttribute<IgnoreAttribute>() != null) continue;
            if (p.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute>() != null) continue;
            if (!p.CanWrite) continue;

            // 3. Column name resolution
            string colName = p.Name;
            var colAttr = p.GetCustomAttribute<ColumnAttribute>();
            if (colAttr != null)
            {
                colName = colAttr.Name;
            }
            else
            {
                var dataColAttr = p.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>();
                if (dataColAttr != null && !string.IsNullOrEmpty(dataColAttr.Name))
                {
                    colName = dataColAttr.Name!;
                }
            }

            // 4. Key resolution
            bool isKey = p.GetCustomAttribute<KeyAttribute>() != null || 
                         p.GetCustomAttribute<System.ComponentModel.DataAnnotations.KeyAttribute>() != null ||
                         p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                         p.Name.Equals($"{type.Name}Id", StringComparison.OrdinalIgnoreCase);
            
            // 5. DatabaseGenerated resolution
            DatabaseGeneratedOption? genOption = null;
            var genAttr = p.GetCustomAttribute<DatabaseGeneratedAttribute>();
            if (genAttr != null)
            {
                genOption = genAttr.Option;
            }
            else
            {
                var dataGenAttr = p.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedAttribute>();
                if (dataGenAttr != null)
                {
                    // Map System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption to Jaunty.Attributes.DatabaseGeneratedOption
                    genOption = (DatabaseGeneratedOption)(int)dataGenAttr.DatabaseGeneratedOption;
                }
            }

            columns.Add(new ColumnMetadata(p, colName, isKey, genOption));
        }

        return new EntityMetadata(tableName, schemaName, columns);
    }
}
