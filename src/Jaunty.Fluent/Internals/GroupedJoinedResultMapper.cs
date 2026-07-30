using System.Globalization;
using System.Reflection;

using Jaunty.Internals.Read;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// Maps a data reader row to a Select projection's result type (anonymous type or DTO),
/// shared by GroupedJoinedQueryBuilder/3/4 - the reflection-based mapping itself doesn't
/// depend on how many entities were joined, only on the already-resolved column aliases.
/// </summary>
internal static class GroupedJoinedResultMapper
{
    /// <summary>
    /// Constructor/property lookups - and, since AUD-R25, column ordinals - resolved once per
    /// query execution and reused across every row, instead of re-running reflection
    /// (GetConstructors/GetProperty) or <c>GetOrdinal</c> per row.
    /// </summary>
    public sealed class ResultMapperPlan
    {
        // Ordinals are stable for the lifetime of a result set, so they are cached here rather
        // than looked up per row - AUD-R25. The reader is tracked alongside them so a plan that is
        // ever reused across result sets re-resolves instead of returning stale ordinals.
        private object? _ordinalsReader;
        private int[]? _ordinals;

        private ResultMapperPlan(ConstructorInfo? constructor, ParameterInfo[]? constructorParameters, PropertyInfo?[]? properties)
        {
            Constructor = constructor;
            ConstructorParameters = constructorParameters;
            Properties = properties;
        }

        public ConstructorInfo? Constructor { get; }
        public ParameterInfo[]? ConstructorParameters { get; }
        public PropertyInfo?[]? Properties { get; }

        /// <summary>
        /// Returns this plan's ordinal buffer for <paramref name="reader"/>, with every slot reset
        /// to -1 ("not yet resolved") when the reader changes. Slots are filled in on first use by
        /// <see cref="MapResult{TResult}"/> rather than eagerly, so an alias the property path
        /// skips - one with no matching writable property - is never looked up at all, exactly as
        /// before.
        /// </summary>
        internal int[] GetOrdinalBuffer(System.Data.IDataReader reader, int length)
        {
            if (ReferenceEquals(_ordinalsReader, reader) && _ordinals is not null && _ordinals.Length == length)
                return _ordinals;

            var ordinals = new int[length];
            for (int i = 0; i < length; i++)
                ordinals[i] = -1;

            _ordinals = ordinals;
            _ordinalsReader = reader;
            return ordinals;
        }

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
        int[] ordinals = plan.GetOrdinalBuffer(reader, aliases.Length);

        if (plan.Constructor is not null)
        {
            var values = new object?[aliases.Length];
            ParameterInfo[] parameters = plan.ConstructorParameters!;

            for (int i = 0; i < aliases.Length; i++)
            {
                int ordinal = ResolveOrdinal(reader, aliases, ordinals, i);

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
                int ordinal = ResolveOrdinal(reader, aliases, ordinals, i);

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

    private static int ResolveOrdinal(System.Data.IDataReader reader, string[] aliases, int[] ordinals, int index)
    {
        int ordinal = ordinals[index];

        if (ordinal < 0)
            ordinals[index] = ordinal = reader.GetOrdinal(aliases[index]);

        return ordinal;
    }

    /// <summary>
    /// Converts a raw ADO.NET value to the target property/parameter type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R26-062. This used to be its own implementation, and its summary claimed the mappers
    /// that share it "can't drift out of sync with each other" - true within this assembly, and
    /// beside the point: two more implementations of the same contract lived in Jaunty and
    /// Jaunty.Extensions.Reflection, and all three disagreed on <see cref="Guid"/>,
    /// <see cref="char"/>, <see cref="Nullable{T}"/> unwrapping and the date/time types.
    /// </para>
    /// <para>
    /// The behaviour now lives in <see cref="DbValueConversion"/>, which documents each divergence
    /// and which way it was settled. Two changes are visible from here: a <see cref="Nullable{T}"/>
    /// target no longer has to be unwrapped by the caller (it was the only one of the three that
    /// required that), and <see cref="DateTimeOffset"/>/<see cref="TimeSpan"/>/<c>DateOnly</c>/
    /// <c>TimeOnly</c> now convert from a string instead of throwing - which is what a SQLite
    /// column returned as ISO-8601-ish text does.
    /// </para>
    /// </remarks>
    public static object ConvertColumnValue(object value, Type targetType) =>
        DbValueConversion.Convert(value, targetType);
}
