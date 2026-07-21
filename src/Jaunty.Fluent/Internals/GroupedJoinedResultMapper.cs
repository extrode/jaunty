using System.Globalization;
using System.Reflection;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// Maps a data reader row to a Select projection's result type (anonymous type or DTO),
/// shared by GroupedJoinedQueryBuilder/3/4 - the reflection-based mapping itself doesn't
/// depend on how many entities were joined, only on the already-resolved column aliases.
/// </summary>
internal static class GroupedJoinedResultMapper
{
    /// <summary>
    /// Constructor/property lookups resolved once per query execution and reused across every
    /// row, instead of re-running reflection (GetConstructors/GetProperty) per row.
    /// </summary>
    public readonly struct ResultMapperPlan
    {
        private ResultMapperPlan(ConstructorInfo? constructor, ParameterInfo[]? constructorParameters, PropertyInfo?[]? properties)
        {
            Constructor = constructor;
            ConstructorParameters = constructorParameters;
            Properties = properties;
        }

        public ConstructorInfo? Constructor { get; }
        public ParameterInfo[]? ConstructorParameters { get; }
        public PropertyInfo?[]? Properties { get; }

        public static ResultMapperPlan Resolve<TResult>(string[] aliases)
        {
            Type resultType = typeof(TResult);

#pragma warning disable IL2090 // Reflection on generic parameter for result mapping
            if (resultType.Name.StartsWith("<>") || resultType.GetConstructors().Any(c => c.GetParameters().Length == aliases.Length))
            {
                ConstructorInfo? constructor = resultType.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == aliases.Length);

                if (constructor is not null)
                    return new ResultMapperPlan(constructor, constructor.GetParameters(), properties: null);
            }

            var properties = new PropertyInfo?[aliases.Length];
            for (int i = 0; i < aliases.Length; i++)
                properties[i] = resultType.GetProperty(aliases[i]);
#pragma warning restore IL2090

            return new ResultMapperPlan(constructor: null, constructorParameters: null, properties);
        }
    }

    public static TResult MapResult<TResult>(System.Data.IDataReader reader, string[] aliases, in ResultMapperPlan plan)
    {
        if (plan.Constructor is not null)
        {
            var values = new object?[aliases.Length];
            ParameterInfo[] parameters = plan.ConstructorParameters!;

            for (int i = 0; i < aliases.Length; i++)
            {
                int ordinal = reader.GetOrdinal(aliases[i]);

                if (!reader.IsDBNull(ordinal))
                {
                    object value = reader.GetValue(ordinal);
                    Type targetType = parameters[i].ParameterType;
                    Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                    values[i] = ConvertColumnValue(value, underlyingType);
                }
            }

            return (TResult)plan.Constructor.Invoke(values);
        }

#pragma warning disable IL2091 // Activator.CreateInstance requires public parameterless constructor
        TResult? instance = Activator.CreateInstance<TResult>();
#pragma warning restore IL2091
        PropertyInfo?[] properties = plan.Properties!;

        for (int i = 0; i < aliases.Length; i++)
        {
            PropertyInfo? property = properties[i];

            if (property is not null && property.CanWrite)
            {
                int ordinal = reader.GetOrdinal(aliases[i]);

                if (!reader.IsDBNull(ordinal))
                {
                    object value = reader.GetValue(ordinal);
                    Type targetType = property.PropertyType;
                    Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                    object converted = ConvertColumnValue(value, underlyingType);
                    property.SetValue(instance, converted);
                }
            }
        }

        return instance!;
    }

    /// <summary>
    /// Converts a raw ADO.NET value to the target property/parameter type. <see cref="Convert.ChangeType(object, Type)"/>
    /// cannot target enum types (always throws <see cref="InvalidCastException"/>) or <see cref="Guid"/>/<see cref="char"/>
    /// from an arbitrary source string, so those are special-cased before falling back to it. Shared by every
    /// reflection-based group/join result mapper (GroupedQueryBuilder, GroupedJoinedQueryBuilder/3/4,
    /// JoinedQueryBuilder&lt;TFrom,TJoin&gt;) so they can't drift out of sync with each other.
    /// </summary>
    public static object ConvertColumnValue(object value, Type targetType)
    {
        if (targetType.IsEnum)
        {
            return value is string enumString
                ? Enum.Parse(targetType, enumString, ignoreCase: true)
                : Enum.ToObject(targetType, Convert.ChangeType(value, Enum.GetUnderlyingType(targetType), CultureInfo.InvariantCulture));
        }

        if (targetType == typeof(Guid))
            return value is Guid guid ? guid : Guid.Parse(value.ToString()!);

        if (targetType == typeof(char) && value is string charString)
            return charString.Length > 0 ? charString[0] : '\0';

        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }
}
