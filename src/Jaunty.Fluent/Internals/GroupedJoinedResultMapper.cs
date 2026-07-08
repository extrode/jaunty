using System.Reflection;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// Maps a data reader row to a Select projection's result type (anonymous type or DTO),
/// shared by GroupedJoinedQueryBuilder/3/4 - the reflection-based mapping itself doesn't
/// depend on how many entities were joined, only on the already-resolved column aliases.
/// </summary>
internal static class GroupedJoinedResultMapper
{
    public static TResult MapResult<TResult>(System.Data.IDataReader reader, string[] aliases)
    {
        Type resultType = typeof(TResult);

#pragma warning disable IL2090 // Reflection on generic parameter for result mapping
        if (resultType.Name.StartsWith("<>") || resultType.GetConstructors().Any(c => c.GetParameters().Length == aliases.Length))
        {
            var values = new object?[aliases.Length];

            ConstructorInfo? constructor = resultType.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == aliases.Length);

            if (constructor is not null)
            {
                ParameterInfo[] parameters = constructor.GetParameters();

                for (int i = 0; i < aliases.Length; i++)
                {
                    int ordinal = reader.GetOrdinal(aliases[i]);

                    if (!reader.IsDBNull(ordinal))
                    {
                        object value = reader.GetValue(ordinal);
                        Type targetType = parameters[i].ParameterType;
                        Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                        values[i] = Convert.ChangeType(value, underlyingType);
                    }
                }

                return (TResult)constructor.Invoke(values);
            }
        }
#pragma warning restore IL2090

#pragma warning disable IL2091 // Activator.CreateInstance requires public parameterless constructor
        TResult? instance = Activator.CreateInstance<TResult>();
#pragma warning restore IL2091
        for (int i = 0; i < aliases.Length; i++)
        {
#pragma warning disable IL2090
            PropertyInfo? property = resultType.GetProperty(aliases[i]);
#pragma warning restore IL2090

            if (property is not null && property.CanWrite)
            {
                int ordinal = reader.GetOrdinal(aliases[i]);

                if (!reader.IsDBNull(ordinal))
                {
                    object value = reader.GetValue(ordinal);
                    Type targetType = property.PropertyType;
                    Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                    object converted = Convert.ChangeType(value, underlyingType);
                    property.SetValue(instance, converted);
                }
            }
        }

        return instance!;
    }
}
