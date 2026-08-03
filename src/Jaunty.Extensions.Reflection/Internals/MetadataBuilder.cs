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
internal static class MetadataBuilder
{
    // Fully qualified type names for System.ComponentModel.DataAnnotations attributes
    private const string TableAttributeTypeName = "System.ComponentModel.DataAnnotations.Schema.TableAttribute";
    private const string ColumnAttributeTypeName = "System.ComponentModel.DataAnnotations.Schema.ColumnAttribute";
    private const string KeyAttributeTypeName = "System.ComponentModel.DataAnnotations.KeyAttribute";
    private const string NotMappedAttributeTypeName = "System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute";
    private const string DatabaseGeneratedAttributeTypeName = "System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedAttribute";

    /// <summary>
    /// Builds entity metadata for the specified type using reflection.
    /// </summary>
    /// <typeparam name="T">The entity type to build metadata for.</typeparam>
    /// <returns>The entity metadata containing table and column information.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the type is abstract.</exception>
    public static EntityMetadata Build<T>()
    {
        Type type = typeof(T);

        if (type.IsAbstract)
            throw new InvalidOperationException($"Type '{type.Name}' cannot be abstract. Only concrete types can be mapped.");

        string? schemaName = JauntyConfig.SchemaNameResolver?.Invoke(type);
        string tableName = JauntyConfig.TableNameResolver?.Invoke(type) ?? type.Name;

        // 1. Table Attribute resolution (Both namespaces)
        TableAttribute? tableAttr = type.GetCustomAttribute<TableAttribute>();
        if (tableAttr is not null)
        {
            if (!string.IsNullOrEmpty(tableAttr.Name)) tableName = tableAttr.Name;

            // AUD-R35-221: this was `is not null`, so [Table("X", "")] set the schema to the empty
            // string and discarded any configured JauntyConfig.SchemaNameResolver value. Every
            // sibling guard tests IsNullOrEmpty - the table name one line above, the
            // DataAnnotations branch below, the column-name guard from AUD-R32-006, and the
            // generator's GetTableNameAndSchema.
            if (!string.IsNullOrEmpty(tableAttr.Schema)) schemaName = tableAttr.Schema;
        }
        else
        {
            // Use string-based detection for System.ComponentModel.DataAnnotations.Schema.TableAttribute
            CustomAttributeData? dataTableAttr = GetAttributeData(type, TableAttributeTypeName);
            if (dataTableAttr is not null)
            {
                object? nameArg = GetConstructorArgument(dataTableAttr, 0) ?? GetNamedArgument(dataTableAttr, "Name");
                object? schemaArg = GetNamedArgument(dataTableAttr, "Schema");

                if (nameArg is string name && !string.IsNullOrEmpty(name))
                    tableName = name;

                if (schemaArg is string schema && !string.IsNullOrEmpty(schema))
                    schemaName = schema;
            }
        }

        PropertyInfo[] props = MostDerivedPerName(type.GetProperties(BindingFlags.Instance | BindingFlags.Public));
        var columns = new List<ColumnMetadata>();

        for (var i = 0; i < props.Length; i++)
        {
            PropertyInfo property = props[i];

            // R16/AUD-R22: an indexer (e.g. "public object this[int i]") surfaces as a public
            // instance property named "Item" with GetIndexParameters().Length > 0.
            // Expression.Property/PropertyInfo.SetValue throw for these; skip them like
            // ParameterCache.cs already does.
            if (property.GetIndexParameters().Length > 0) continue;

            // 2. Ignore resolution
            if (property.GetCustomAttribute<IgnoreAttribute>() is not null) continue;

            // Use string-based detection for NotMappedAttribute
            if (HasAttribute(property, NotMappedAttributeTypeName)) continue;

            if (!property.CanWrite) continue;

            // 3. Column name resolution
            string colName = JauntyConfig.ColumnNameResolver?.Invoke(property.Name) ?? property.Name;
            ColumnAttribute? colAttr = property.GetCustomAttribute<ColumnAttribute>();

            // AUD-R32-006: the IsNullOrEmpty guard was missing here alone. ColumnAttribute's
            // constructor rejects null but not "", so [Column("")] mapped the property to an
            // empty column name, while the DataAnnotations-compat path below and the table/schema
            // resolutions above all fall back to the default. The source generator carries the
            // same guard so both mapping modes agree.
            if (colAttr is not null && !string.IsNullOrEmpty(colAttr.Name))
                colName = colAttr.Name;
            else
            {
                // Use string-based detection for ColumnAttribute
                CustomAttributeData? dataColAttr = GetAttributeData(property, ColumnAttributeTypeName);
                if (dataColAttr is not null)
                {
                    var nameArg = GetConstructorArgument(dataColAttr, 0) ?? GetNamedArgument(dataColAttr, "Name");
                    if (nameArg is string name && !string.IsNullOrEmpty(name))
                        colName = name;
                }
            }

            // 4. Key resolution
            bool isKey = property.GetCustomAttribute<KeyAttribute>() is not null ||
                         HasAttribute(property, KeyAttributeTypeName) ||
                         property.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                         property.Name.Equals($"{type.Name}Id", StringComparison.OrdinalIgnoreCase);

            // 5. DatabaseGenerated resolution
            //
            // AUD-R25: note that this path infers nothing. With no [DatabaseGenerated], genOption
            // stays null and ColumnMetadata sets IsIdentity = false, so a conventional `int Id` key
            // stays in InsertColumns and its value is sent on INSERT.
            //
            // JauntyGenerator does the opposite for the same entity: it treats a single int/long
            // key with no [DatabaseGenerated] as an identity column and omits it. The key
            // convention itself is shared and identical (see step 4 above and JauntyGenerator's
            // isKey), so the two paths agree on which column is the key and disagree only on
            // whether the database generates it. DrDispatcher prefers the generated mapper when one
            // exists, which means adding or removing the Jaunty.SourceGenerator package reference
            // silently changes the INSERT for such an entity - dropping a client-assigned key on
            // one side, or overriding a real sequence on the other.
            //
            // Converging them is a product decision rather than an audit fix: either direction
            // changes the SQL of existing entities on one of the two paths, and both behaviours are
            // relied on by current tests. Both are pinned by tests so the divergence cannot drift
            // further unnoticed, and documented in docs/01-api-reference/attributes.md, which now
            // tells users to write [DatabaseGenerated] explicitly to get identical behaviour either
            // way.
            DatabaseGeneratedOption? genOption = null;
            DatabaseGeneratedAttribute? genAttr = property.GetCustomAttribute<DatabaseGeneratedAttribute>();

            if (genAttr is not null)
                genOption = genAttr.Option;
            else
            {
                // Use string-based detection for DatabaseGeneratedAttribute
                CustomAttributeData? dataGenAttr = GetAttributeData(property, DatabaseGeneratedAttributeTypeName);
                if (dataGenAttr is not null)
                {
                    // Get the DatabaseGeneratedOption enum value from the attribute
                    object? optionArg = GetConstructorArgument(dataGenAttr, 0) ?? GetNamedArgument(dataGenAttr, "DatabaseGeneratedOption");

                    if (optionArg is not null)
                    {
                        // Map System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption to Jaunty.Attributes.DatabaseGeneratedOption
                        // Both enums have the same underlying values: None=0, Identity=1, Computed=2
                        genOption = (DatabaseGeneratedOption)Convert.ToInt32(optionArg);
                    }
                }
            }

            columns.Add(new ColumnMetadata(property, colName, isKey, genOption));
        }

        return new EntityMetadata(tableName, schemaName, columns);
    }

    /// <summary>
    /// Gets attribute data by type name without requiring a hard reference to the attribute type.
    /// </summary>
    private static CustomAttributeData? GetAttributeData(MemberInfo member, string attributeTypeName)
    {

        foreach (CustomAttributeData? Attr in member.GetCustomAttributesData())
            if (Attr.AttributeType.FullName == attributeTypeName) return Attr;

        return null;
    }

    /// <summary>
    /// Checks if a member has an attribute by type name without requiring a hard reference.
    /// </summary>
    /// <summary>
    /// Keeps one property per name, the most derived one, preserving declaration order.
    /// </summary>
    /// <remarks>
    /// AUD-R35-220. A type-changing <c>new</c> shadow - <c>class B { public string P { get; set; } }</c>
    /// with <c>class D : B { public new int P { get; set; } }</c> - makes
    /// <c>GetProperties(Instance | Public)</c> return two <c>PropertyInfo</c>s named <c>P</c> (a
    /// same-signature <c>new</c> returns only the derived one, so only the type-changing case is
    /// affected). Both used to reach <c>ColumnMetadata</c>, and <c>ThrowIfDuplicateColumnNames</c>
    /// then reported <c>"'P' and 'P'"</c>, which reads as a nonsense diagnostic. The generator's
    /// <c>GetMappableProperties</c> and <c>MappedPropertyFilter</c> in Jaunty.FlatFiles.DuckDB both
    /// dedup by name already; the reflection path was the only one of the three that did not.
    /// Indexers are passed through untouched rather than deduped: two of them - <c>this[int]</c>
    /// and <c>this[string]</c> - both surface as <c>Item</c>, and the caller skips them by
    /// <c>GetIndexParameters</c> rather than by name, so they must reach it unchanged.
    /// </remarks>
    private static PropertyInfo[] MostDerivedPerName(PropertyInfo[] properties)
    {
        var indexByName = new Dictionary<string, int>(properties.Length, StringComparer.Ordinal);
        var kept = new List<PropertyInfo>(properties.Length);

        for (var i = 0; i < properties.Length; i++)
        {
            PropertyInfo property = properties[i];

            if (property.GetIndexParameters().Length > 0)
            {
                kept.Add(property);
                continue;
            }

            if (!indexByName.TryGetValue(property.Name, out int index))
            {
                indexByName[property.Name] = kept.Count;
                kept.Add(property);
                continue;
            }

            if (IsMoreDerived(property, kept[index]))
                kept[index] = property;
        }

        return kept.Count == properties.Length ? properties : kept.ToArray();
    }

    /// <summary>
    /// True when <paramref name="candidate"/> is declared on a type derived from the one that
    /// declares <paramref name="incumbent"/>.
    /// </summary>
    private static bool IsMoreDerived(PropertyInfo candidate, PropertyInfo incumbent)
    {
        Type? candidateType = candidate.DeclaringType;
        Type? incumbentType = incumbent.DeclaringType;

        return candidateType is not null
            && incumbentType is not null
            && candidateType != incumbentType
            && incumbentType.IsAssignableFrom(candidateType);
    }

    private static bool HasAttribute(MemberInfo member, string attributeTypeName)
    {

        foreach (CustomAttributeData? Attr in member.GetCustomAttributesData())
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
        foreach (CustomAttributeNamedArgument Arg in attributeData.NamedArguments)
        {
            if (Arg.MemberName == argumentName)
            {
                return Arg.TypedValue.Value;
            }
        }

        return null;
    }
}