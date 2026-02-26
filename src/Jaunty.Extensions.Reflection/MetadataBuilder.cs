using System;
using System.Collections.Generic;
using System.Reflection;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Internals.Entity;

namespace Jaunty.Extensions.Reflection;

/// <summary>
/// Builds entity metadata using reflection.
/// </summary>
/// <remarks>
/// This extension uses runtime reflection and is not compatible with NativeAOT.
/// For NativeAOT scenarios, use the source generator instead.
/// </remarks>
public static class MetadataBuilder
{
    // Fully qualified type names for System.ComponentModel.DataAnnotations attributes
    private const string TableAttributeTypeName = "System.ComponentModel.DataAnnotations.Schema.TableAttribute";
    private const string ColumnAttributeTypeName = "System.ComponentModel.DataAnnotations.Schema.ColumnAttribute";
    private const string KeyAttributeTypeName = "System.ComponentModel.DataAnnotations.KeyAttribute";
    private const string NotMappedAttributeTypeName = "System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute";
    private const string DatabaseGeneratedAttributeTypeName = "System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedAttribute";

    public static EntityMetadata Build<T>()
    {
        var type = typeof(T);

        if (type.IsAbstract)
            throw new InvalidOperationException($"Type '{type.Name}' cannot be abstract. Only concrete types can be mapped.");

        string? schemaName = JauntyConfig.SchemaNameResolver?.Invoke(type);
        string tableName = JauntyConfig.TableNameResolver?.Invoke(type) ?? type.Name;

        // 1. Table Attribute resolution (Both namespaces)
        var tableAttr = type.GetCustomAttribute<TableAttribute>();
        if (tableAttr is not null)
        {
            tableName = tableAttr.Name;
            if (tableAttr.Schema is not null) schemaName = tableAttr.Schema;
        }
        else
        {
            // Use string-based detection for System.ComponentModel.DataAnnotations.Schema.TableAttribute
            var dataTableAttr = GetAttributeData(type, TableAttributeTypeName);
            if (dataTableAttr != null)
            {
                object? nameArg = GetConstructorArgument(dataTableAttr, 0) ?? GetNamedArgument(dataTableAttr, "Name");
                object? schemaArg = GetNamedArgument(dataTableAttr, "Schema");

                if (nameArg is string name && !string.IsNullOrEmpty(name))
                    tableName = name;
                if (schemaArg is string schema && !string.IsNullOrEmpty(schema))
                    schemaName = schema;
            }
        }

        var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var columns = new List<ColumnMetadata>();

        foreach (var p in props)
        {
            // 2. Ignore resolution
            if (p.GetCustomAttribute<IgnoreAttribute>() != null) continue;

            // Use string-based detection for NotMappedAttribute
            if (HasAttribute(p, NotMappedAttributeTypeName)) continue;

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
                // Use string-based detection for ColumnAttribute
                var dataColAttr = GetAttributeData(p, ColumnAttributeTypeName);
                if (dataColAttr != null)
                {
                    var nameArg = GetConstructorArgument(dataColAttr, 0) ?? GetNamedArgument(dataColAttr, "Name");
                    if (nameArg is string name && !string.IsNullOrEmpty(name))
                    {
                        colName = name;
                    }
                }
            }

            // 4. Key resolution
            bool isKey = p.GetCustomAttribute<KeyAttribute>() != null ||
                         HasAttribute(p, KeyAttributeTypeName) ||
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
                // Use string-based detection for DatabaseGeneratedAttribute
                var dataGenAttr = GetAttributeData(p, DatabaseGeneratedAttributeTypeName);
                if (dataGenAttr != null)
                {
                    // Get the DatabaseGeneratedOption enum value from the attribute
                    var optionArg = GetConstructorArgument(dataGenAttr, 0) ?? GetNamedArgument(dataGenAttr, "DatabaseGeneratedOption");
                    if (optionArg != null)
                    {
                        // Map System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption to Jaunty.Attributes.DatabaseGeneratedOption
                        // Both enums have the same underlying values: None=0, Identity=1, Computed=2
                        genOption = (DatabaseGeneratedOption)Convert.ToInt32(optionArg);
                    }
                }
            }

            columns.Add(new ColumnMetadata(p, colName, isKey, genOption));
        }

        return new EntityMetadata(tableName, schemaName, columns);
    }

    /// <summary>
    /// Gets attribute data by type name without requiring a hard reference to the attribute type.
    /// </summary>
    private static CustomAttributeData? GetAttributeData(MemberInfo member, string attributeTypeName)
    {
        foreach (var Attr in member.GetCustomAttributesData())
            if (Attr.AttributeType.FullName == attributeTypeName) return Attr;

        return null;
    }

    /// <summary>
    /// Checks if a member has an attribute by type name without requiring a hard reference.
    /// </summary>
    private static bool HasAttribute(MemberInfo member, string attributeTypeName)
    {
        foreach (var Attr in member.GetCustomAttributesData())
        {
            if (Attr.AttributeType.FullName == attributeTypeName) return true;
        }

        return false;
    }

    /// <summary>
    /// Gets a constructor argument from CustomAttributeData by index.
    /// </summary>
    private static object? GetConstructorArgument(CustomAttributeData attributeData, int index)
    {
        return index < attributeData.ConstructorArguments.Count ? attributeData.ConstructorArguments[index].Value : null;
    }

    /// <summary>
    /// Gets a named argument from CustomAttributeData by name.
    /// </summary>
    private static object? GetNamedArgument(CustomAttributeData attributeData, string argumentName)
    {
        var namedArg = new CustomAttributeNamedArgument();

        foreach (var Arg in attributeData.NamedArguments)
        {
            if (Arg.MemberName == argumentName)
            {
                namedArg = Arg;
                break;
            }
        }

        return namedArg.TypedValue.Value;
    }
}
