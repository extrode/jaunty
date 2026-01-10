using System.Reflection;

using Jaunty.Attributes;
using Jaunty.Configuration;

namespace Jaunty.Internals.Entity;

internal static class MetadataBuilder
{
    public static EntityMetadata Build<T>()
    {
        var type = typeof(T);

        if (type.IsAbstract)
            throw new InvalidOperationException($"{type.FullName} cannot be abstract.");

        string? schemaName = null;
        string tableName = type.Name;

        schemaName ??= JauntyConfig.SchemaNameResolver?.Invoke(type);
        tableName = JauntyConfig.TableNameResolver?.Invoke(type) ?? tableName;

        var (schemaAttr, tableAttr) = AttributeHelper.GetTableSchemaAndName(type);
        schemaName = schemaAttr ?? schemaName;
        tableName = tableAttr ?? tableName;

        // Resolve Columns
        PropertyInfo[] propertyInfos = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        List<ColumnMetadata> columns = new(propertyInfos.Length >> 1); // we will allocate less for non-writable + ignored properties
        Func<string, string>? columnNameResolver = JauntyConfig.ColumnNameResolver;

        for (int i = 0; i < propertyInfos.Length; i++)
        {
            PropertyInfo propertyInfo = propertyInfos[i];

            // Skip indexers
            if (propertyInfo.GetIndexParameters().Length != 0)
                continue;

            // Skip non-writable 
            if (!propertyInfo.CanWrite)
                continue;

            // Skip ignored properties
            if (AttributeHelper.HasIgnoreAttribute(propertyInfo))
                continue;

            string columnName = ResolveColumnName(propertyInfo, columnNameResolver);

            bool isKey = AttributeHelper.HasKeyAttribute(propertyInfo) ||
                propertyInfo.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                propertyInfo.Name.Equals($"{type.Name}Id", StringComparison.OrdinalIgnoreCase);

            DatabaseGeneratedOption? genOption = AttributeHelper.GetDatabaseGeneratedOption(propertyInfo);
            columns.Add(new ColumnMetadata(propertyInfo, columnName, isKey, genOption));
        }

        return new EntityMetadata(tableName, schemaName, columns);
    }

    private static string ResolveColumnName(PropertyInfo property, Func<string, string>? resolver)
    {
        string columnName = property.Name;
        if (resolver is not null)
            columnName = resolver(columnName);
        return AttributeHelper.GetColumnName(property) ?? columnName;
    }
}
