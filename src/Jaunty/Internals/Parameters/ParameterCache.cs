using System.Collections.Concurrent;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Linq.Expressions;
using System.Reflection;

namespace Jaunty.Internals.Parameters;

/// <summary>
/// Caches parameter metadata for entity types used in parameter binding.
/// </summary>
/// <remarks>
/// <para>
/// This cache stores metadata about public properties that can be bound to SQL parameters.
/// The metadata includes property names and compiled getter delegates for efficient value extraction.
/// </para>
/// <para>
/// Uses <see cref="ConcurrentDictionary{TKey, TValue}"/> for thread-safe caching across multiple threads.
/// </para>
/// </remarks>
internal static class ParameterCache
{
    /// <summary>
    /// The cache mapping entity types to their parameter metadata arrays.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, ParameterMetadata[]> Cache = new();

    /// <summary>
    /// Gets the cached parameter metadata for the specified type, or builds it if not cached.
    /// </summary>
    /// <param name="type">The entity type to get parameter metadata for.</param>
    /// <returns>An array of parameter metadata for all public properties.</returns>
    public static ParameterMetadata[] Get(
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
        Type type)
    {
        return Cache.GetOrAdd(type, BuildMetadata);
    }

    /// <summary>
    /// Builds parameter metadata for all public properties of the specified type.
    /// </summary>
    /// <param name="type">The entity type to build metadata for.</param>
    /// <returns>An array of parameter metadata, one per public property.</returns>
#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2070", Justification = "Parameters object properties may be trimmed under NativeAOT if the type isn't otherwise rooted; this is called for any named or anonymous parameters type passed to Query/Execute APIs, not just anonymous types. Suppressed pending a source-generated parameter-binding path (see MappedCache.cs's analogous ReadEntity limitation); callers using NativeAOT publish today must ensure their parameter POCOs are otherwise rooted until that lands.")]
#endif
    private static ParameterMetadata[] BuildMetadata(
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
        Type type)
    {
        PropertyInfo[] props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var result = new ParameterMetadata[props.Length];

        for (int i = 0; i < props.Length; i++)
        {
            PropertyInfo p = props[i];
            result[i] = new ParameterMetadata(p.Name, CreateGetter(p), property: p);
        }

        return result;
    }

    /// <summary>
    /// Creates a compiled getter delegate for the specified property.
    /// </summary>
    /// <param name="prop">The property to create a getter for.</param>
    /// <returns>A delegate that extracts the property value from an object instance.</returns>
    /// <remarks>
    /// Uses expression trees to create a strongly-typed, compiled delegate for optimal performance.
    /// </remarks>
    private static Func<object, object?> CreateGetter(PropertyInfo prop)
    {
        ParameterExpression obj = Expression.Parameter(typeof(object), "o");
        UnaryExpression cast = Expression.Convert(obj, prop.DeclaringType!);
        MemberExpression access = Expression.Property(cast, prop);
        UnaryExpression box = Expression.Convert(access, typeof(object));

        return Expression.Lambda<Func<object, object?>>(box, obj).Compile();
    }
}
