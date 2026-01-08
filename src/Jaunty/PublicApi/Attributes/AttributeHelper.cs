using System.Reflection;

namespace Jaunty.PublicApi.Attributes;

internal static class AttributeHelper
{
    public static (string?, string?) GetTableSchemaAndName(Type type)
    {
        Attribute[] attributes = Attribute.GetCustomAttributes(type);
        for (int i = 0; i < attributes.Length; i++)
        {
            if (attributes[i] is TableAttribute tableAttribute)
                return (tableAttribute.Name, tableAttribute.Schema);
#if NET8_0_OR_GREATER
            if (attributes[i] is System.ComponentModel.DataAnnotations.Schema.TableAttribute sysTableAttribute)
                return (sysTableAttribute.Name, sysTableAttribute.Schema);
#else
            Type attributeType = attributes[i].GetType();
            if (attributeType.FullName == "System.ComponentModel.DataAnnotations.Schema.TableAttribute")
            {
                var name = attributeType.GetProperty("Name")?.GetValue(attributes[i]) as string;
                var schema = attributeType.GetProperty("Schema")?.GetValue(attributes[i]) as string;
                return (name, schema);
            }
#endif
        }

        return (null, null);
    }

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