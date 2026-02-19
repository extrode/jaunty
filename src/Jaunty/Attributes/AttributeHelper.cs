using System.Reflection;

namespace Jaunty.Attributes;

/// <summary>
/// Internal helper class for reading entity mapping attributes.
/// </summary>
/// <remarks>
/// <para>
/// This class provides utility methods for reading Jaunty's mapping attributes as well as 
/// standard .NET attributes from <see cref="System.ComponentModel.DataAnnotations"/> and 
/// <see cref="System.ComponentModel.DataAnnotations.Schema"/> namespaces.
/// </para>
/// <para>
/// This allows Jaunty to work with both its native attributes and standard .NET data 
/// annotation attributes, providing flexibility for users who prefer standard attributes.
/// </para>
/// </remarks>
internal static class AttributeHelper
{
    /// <summary>
    /// Gets the table name and schema from a type's table attribute.
    /// </summary>
    /// <param name="type">The type to inspect for table attributes.</param>
    /// <returns>
    /// A tuple containing the schema (first) and table name (second), or (null, null) if no table attribute is found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method checks for both Jaunty's <see cref="TableAttribute"/> and the standard 
    /// <see cref="System.ComponentModel.DataAnnotations.Schema.TableAttribute"/>.
    /// </para>
    /// </remarks>
    public static (string?, string?) GetTableSchemaAndName(Type type)
    {
        Attribute[] attributes = Attribute.GetCustomAttributes(type);
        for (int i = 0; i < attributes.Length; i++)
        {
            if (attributes[i] is TableAttribute tableAttribute)
                return (tableAttribute.Schema, tableAttribute.Name);
#if NET8_0_OR_GREATER
            if (attributes[i] is System.ComponentModel.DataAnnotations.Schema.TableAttribute sysTableAttribute)
                return (sysTableAttribute.Schema, sysTableAttribute.Name);
#else
            Type attributeType = attributes[i].GetType();
            if (attributeType.FullName == "System.ComponentModel.DataAnnotations.Schema.TableAttribute")
            {
                var name = attributeType.GetProperty("Name")?.GetValue(attributes[i]) as string;
                var schema = attributeType.GetProperty("Schema")?.GetValue(attributes[i]) as string;
                return (schema, name);
            }
#endif
        }

        return (null, null);
    }

    /// <summary>
    /// Gets the column name from a property's column attribute.
    /// </summary>
    /// <param name="property">The property to inspect for column attributes.</param>
    /// <returns>The column name, or null if no column attribute is found.</returns>
    /// <remarks>
    /// <para>
    /// This method checks for both Jaunty's <see cref="ColumnAttribute"/> and the standard 
    /// <see cref="System.ComponentModel.DataAnnotations.Schema.ColumnAttribute"/>.
    /// </para>
    /// </remarks>
    public static string? GetColumnName(PropertyInfo property)
    {
        Attribute[] attributes = Attribute.GetCustomAttributes(property);
        for (int i = 0; i < attributes.Length; i++)
        {
            if (attributes[i] is ColumnAttribute columnAttribute)
                return columnAttribute.Name;
#if NET8_0_OR_GREATER
            if (attributes[i] is System.ComponentModel.DataAnnotations.Schema.ColumnAttribute sysColumnAttribute)
                return sysColumnAttribute.Name;
#else
            Type attributeType = attributes[i].GetType();
            if (attributeType.FullName == "System.ComponentModel.DataAnnotations.Schema.ColumnAttribute")
                return attributeType.GetProperty("Name")?.GetValue(attributes[i]) as string;
#endif
        }

        return null;
    }

    /// <summary>
    /// Checks if a property has a primary key attribute.
    /// </summary>
    /// <param name="property">The property to inspect.</param>
    /// <returns>True if the property has a key attribute; otherwise, false.</returns>
    /// <remarks>
    /// <para>
    /// This method checks for both Jaunty's <see cref="KeyAttribute"/> and the standard 
    /// <see cref="System.ComponentModel.DataAnnotations.KeyAttribute"/>.
    /// </para>
    /// </remarks>
    public static bool HasKeyAttribute(PropertyInfo property)
    {
        Attribute[] attributes = Attribute.GetCustomAttributes(property);
        for (int i = 0; i < attributes.Length; i++)
        {
            if (attributes[i] is KeyAttribute)
                return true;
#if NET8_0_OR_GREATER
            if (attributes[i] is System.ComponentModel.DataAnnotations.KeyAttribute)
                return true;
#else
            Type attributeType = attributes[i].GetType();
            if (attributeType.FullName == "System.ComponentModel.DataAnnotations.KeyAttribute")
                return true;
#endif
        }

        return false;
    }

    /// <summary>
    /// Checks if a property has an ignore attribute.
    /// </summary>
    /// <param name="property">The property to inspect.</param>
    /// <returns>True if the property has an ignore attribute; otherwise, false.</returns>
    /// <remarks>
    /// <para>
    /// This method checks for both Jaunty's <see cref="IgnoreAttribute"/> and the standard 
    /// <see cref="System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute"/>.
    /// </para>
    /// </remarks>
    public static bool HasIgnoreAttribute(PropertyInfo property)
    {
        Attribute[] attributes = Attribute.GetCustomAttributes(property);
        for (int i = 0; i < attributes.Length; i++)
        {
            if (attributes[i] is IgnoreAttribute)
                return true;
#if NET8_0_OR_GREATER
            if (attributes[i] is System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute)
                return true;
#else
            Type attributeType = attributes[i].GetType();
            if (attributeType.FullName == "System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute")
                return true;
#endif
        }

        return false;
    }

    /// <summary>
    /// Gets the database generation option for a property.
    /// </summary>
    /// <param name="property">The property to inspect.</param>
    /// <returns>
    /// The database generation option if specified; otherwise, null.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method checks for both Jaunty's <see cref="DatabaseGeneratedAttribute"/> and the standard 
    /// <see cref="System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedAttribute"/>.
    /// </para>
    /// </remarks>
    public static DatabaseGeneratedOption? GetDatabaseGeneratedOption(PropertyInfo property)
    {
        Attribute[] attributes = Attribute.GetCustomAttributes(property);
        for (int i = 0; i < attributes.Length; i++)
        {
            if (attributes[i] is DatabaseGeneratedAttribute dbGenerated)
                return dbGenerated.Option;
#if NET8_0_OR_GREATER
            if (attributes[i] is System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedAttribute sysDbGenerated)
            {
                int value = (int)sysDbGenerated.GetType().GetProperty("DatabaseGeneratedOption")?.GetValue(sysDbGenerated)!;
                return (DatabaseGeneratedOption)value;
            }
#else
            Type attributeType = attributes[i].GetType();
            if (attributeType.FullName == "System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedAttribute")
            {
                int value = (int)attributeType.GetProperty("DatabaseGeneratedOption")?.GetValue(attributes[i])!;
                return (DatabaseGeneratedOption)value;
            }
#endif
        }

        return null;
    }
}
