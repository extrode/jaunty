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

        private ResultMapperPlan(
            ConstructorInfo? constructor,
            ParameterInfo[]? constructorParameters,
            PropertyInfo?[]? properties,
            int[]? parameterAliasOrder = null,
            bool isScalar = false)
        {
            Constructor = constructor;
            ConstructorParameters = constructorParameters;
            Properties = properties;
            ParameterAliasOrder = parameterAliasOrder;
            IsScalar = isScalar;
        }

        /// <summary>
        /// AUD-R34-005. A single-column projection into a scalar result type - the shape of
        /// <c>.Select(g =&gt; g.Count())</c> or <c>.Select(g =&gt; g.Key)</c> - has no member to bind
        /// the column to. <see cref="int"/> declares no constructors and no <c>Value</c> property,
        /// so it fell through to <c>Activator.CreateInstance</c> and every row mapped to 0; a
        /// <see cref="string"/> instead matched one of its own arity-1 constructors and blew up
        /// inside it. Reading the one column and converting it is what both of those shapes mean.
        /// </summary>
        public bool IsScalar { get; }

        public ConstructorInfo? Constructor { get; }
        public ParameterInfo[]? ConstructorParameters { get; }
        public PropertyInfo?[]? Properties { get; }

        /// <summary>
        /// For the constructor path: <c>ParameterAliasOrder[p]</c> is the index of the alias that
        /// feeds constructor parameter <c>p</c>. Null when the constructor's parameter names do not
        /// all correspond to aliases, in which case binding stays positional - see
        /// <see cref="Resolve{TResult}"/>.
        /// </summary>
        public int[]? ParameterAliasOrder { get; }

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

            if (aliases.Length == 1 && IsScalarResult(resultType))
                return new ResultMapperPlan(constructor: null, constructorParameters: null, properties: null, parameterAliasOrder: null, isScalar: true);

#pragma warning disable IL2090 // Reflection on generic parameter for result mapping
            if (resultType.Name.StartsWith("<>") || resultType.GetConstructors().Any(c => c.GetParameters().Length == aliases.Length))
            {
                ConstructorInfo? constructor = resultType.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == aliases.Length);

                if (constructor is not null)
                {
                    ParameterInfo[] parameters = constructor.GetParameters();
                    return new ResultMapperPlan(constructor, parameters, properties: null, BuildParameterAliasOrder(parameters, aliases));
                }
            }

            var properties = new PropertyInfo?[aliases.Length];
            for (int i = 0; i < aliases.Length; i++)
                properties[i] = resultType.GetProperty(aliases[i]);
#pragma warning restore IL2090

            return new ResultMapperPlan(constructor: null, constructorParameters: null, properties);
        }

        /// <summary>
        /// Maps each constructor parameter to the alias of the same name (case-insensitively),
        /// so a projection's columns reach the members that share their names rather than whatever
        /// happens to sit at the same position - AUD-R31. An anonymous type's compiler-generated
        /// constructor always mirrors declaration order, so this is a no-op for it; a hand-written
        /// DTO or record whose constructor declares the same names in a different order used to be
        /// bound positionally and silently mis-populated.
        /// <para>
        /// Returns null when the names do not all line up, which keeps a constructor whose
        /// parameters are named unlike the aliases entirely on the previous positional behaviour -
        /// positional is the only way such a type could ever have been bound.
        /// </para>
        /// </summary>
        private static int[]? BuildParameterAliasOrder(ParameterInfo[] parameters, string[] aliases)
        {
            var order = new int[parameters.Length];
            bool reordered = false;

            for (int p = 0; p < parameters.Length; p++)
            {
                string? name = parameters[p].Name;
                int aliasIndex = -1;

                for (int a = 0; a < aliases.Length; a++)
                {
                    if (string.Equals(aliases[a], name, StringComparison.OrdinalIgnoreCase))
                    {
                        aliasIndex = a;
                        break;
                    }
                }

                if (aliasIndex < 0)
                    return null;

                order[p] = aliasIndex;
                reordered |= aliasIndex != p;
            }

            return reordered ? order : null;
        }
    }

    public static TResult MapResult<TResult>(System.Data.IDataReader reader, string[] aliases, in ResultMapperPlan plan)
    {
        int[] ordinals = plan.GetOrdinalBuffer(reader, aliases.Length);

        if (plan.IsScalar)
        {
            int scalarOrdinal = ResolveOrdinal(reader, aliases, ordinals, 0);

            if (reader.IsDBNull(scalarOrdinal))
            {
                // AUD-R35-065. This returned default! unconditionally, so
                // .Select(g => g.Max(x => x.Price)) typed as a non-nullable decimal/int yielded 0
                // for a group whose aggregate is NULL - inventing data, and indistinguishable from
                // a real zero. The constructor path immediately below decided the opposite for
                // exactly this case (AUD-R33-005), so the two paths in this one method disagreed on
                // what NULL into a non-nullable value-type target means. A NULL aggregate is what
                // MAX/MIN/SUM return over an all-NULL group, so the case is reachable, not
                // theoretical. Reference types and Nullable<T> keep returning null.
                if (typeof(TResult).IsValueType && Nullable.GetUnderlyingType(typeof(TResult)) is null)
                {
                    throw new InvalidOperationException(
                        $"Column '{aliases[0]}' is NULL but the projection type is the non-nullable " +
                        $"value type '{typeof(TResult).Name}'. Project to a nullable type, or use a " +
                        $"NULL-handling function such as Sql.Coalesce so the aggregate cannot be NULL.");
                }

                return default!;
            }

            Type scalarType = Nullable.GetUnderlyingType(typeof(TResult)) ?? typeof(TResult);
            return (TResult)ConvertColumnValue(reader.GetValue(scalarOrdinal), scalarType);
        }

        if (plan.Constructor is not null)
        {
            var values = new object?[aliases.Length];
            ParameterInfo[] parameters = plan.ConstructorParameters!;
            int[]? order = plan.ParameterAliasOrder;

            for (int p = 0; p < parameters.Length; p++)
            {
                int aliasIndex = order is null ? p : order[p];
                int ordinal = ResolveOrdinal(reader, aliases, ordinals, aliasIndex);

                Type targetType = parameters[p].ParameterType;

                if (!reader.IsDBNull(ordinal))
                {
                    object value = reader.GetValue(ordinal);
                    Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                    values[p] = ConvertColumnValue(value, underlyingType);
                }
                else if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null)
                {
                    // AUD-R33-005. A NULL column against a non-nullable value-type parameter left
                    // the slot null and reached Constructor.Invoke, which throws an opaque
                    // ArgumentException naming neither the column nor the parameter - so the caller
                    // saw a reflection error with nothing in it to act on.
                    //
                    // Substituting the type's default would match the property path, which simply
                    // skips the assignment, but there is no way to box the default of an arbitrary
                    // value type that survives AOT: Activator.CreateInstance trips IL2072 (its
                    // PublicParameterlessConstructor annotation is not carried by ParameterType) and
                    // Array.CreateInstance trips IL3050 (RequiresDynamicCode). Throwing is the
                    // defined outcome that is available, and it is the better one here anyway - a
                    // constructor parameter, unlike a settable property, is a value the type
                    // declared it requires, so silently substituting a zero would be inventing data.
                    throw new InvalidOperationException(
                        $"Column '{aliases[aliasIndex]}' is NULL but constructor parameter " +
                        $"'{parameters[p].Name}' of '{typeof(TResult).Name}' is the non-nullable " +
                        $"value type '{targetType.Name}'. Make the parameter nullable, or project " +
                        $"a non-NULL value for that column.");
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

    /// <summary>
    /// A result type that a single column maps to whole, rather than one whose members the
    /// aliases bind to. Deliberately the same set <c>ParameterBinder.IsScalarType</c> treats as a
    /// bare parameter value, so "what counts as a scalar" means one thing across the codebase;
    /// it is private there, and this assembly needs no more than the predicate.
    /// </summary>
    private static bool IsScalarResult(Type type)
    {
        Type underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
            || underlying.IsEnum
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(TimeSpan)
            || underlying == typeof(Guid)
            || underlying == typeof(byte[])
#if NET8_0_OR_GREATER
            || underlying == typeof(DateOnly)
            || underlying == typeof(TimeOnly)
#endif
            ;
    }
}
