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
    /// <remarks>
    /// Spec 011. This used to declare
    /// <c>[DynamicallyAccessedMembers(PublicProperties)]</c> on <paramref name="type"/>. The
    /// annotation was removed because it was not true and could not become true: every caller reaches
    /// here through <c>parameters.GetType()</c> on an <c>object</c>, which carries no annotation, so
    /// nothing was ever propagated. Its only effect was to relocate the problem - two IL2072 warnings
    /// at the call sites for failing to satisfy a requirement no caller can satisfy, plus an IL2111
    /// here for handing the annotated <see cref="BuildMetadata"/> to <c>GetOrAdd</c> as a delegate.
    /// Three warnings, no preservation. Actual preservation is arranged at the consumer's call sites
    /// by generated rooting; see <c>Jaunty.JauntyAot</c> and <c>JAUNTYGEN003</c>.
    /// </remarks>
    public static ParameterMetadata[] Get(Type type)
    {
        // Called directly rather than as a `GetOrAdd(type, BuildMetadata)` method group: passing a
        // method whose parameter is annotated as a delegate is what produced IL2111, and the trimmer
        // is right that it cannot see through a delegate. Behaviour is unchanged - the factory
        // overload holds no lock either, so a concurrent double-build was always possible.
        if (Cache.TryGetValue(type, out ParameterMetadata[]? cached))
            return cached;

        return Cache.GetOrAdd(type, BuildMetadata(type));
    }

    /// <summary>
    /// Builds parameter metadata for all public properties of the specified type.
    /// </summary>
    /// <param name="type">The entity type to build metadata for.</param>
    /// <returns>An array of parameter metadata, one per public property.</returns>
    /// <remarks>
    /// The one place in the parameter path that actually reflects, and so the one honest place for the
    /// suppression. Spec 011 arranges preservation from outside: the source generator reads the
    /// consumer's <c>Query</c>/<c>Execute</c> call sites and emits
    /// <c>JauntyAot.PreserveParameters&lt;T&gt;()</c> for each parameters type into a module
    /// initializer, so the getters this enumerates are statically required by the consumer's own
    /// assembly. Where it cannot - an <c>object</c>-typed variable, a type built by reflection - it
    /// reports <c>JAUNTYGEN003</c> at that call site instead of letting the failure surface after
    /// publish.
    /// </remarks>
#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2070", Justification = "The type arrives as parameters.GetType() from an object, so no annotation can flow here and none is declared (spec 011 removed the one that used to be, because it produced three warnings and preserved nothing). Preservation is arranged at the consumer's call sites: the source generator emits JauntyAot.PreserveParameters<T>() for every parameters type it can see there. Call sites it cannot see are reported as JAUNTYGEN003 rather than left to fail at runtime.")]
#endif
    private static ParameterMetadata[] BuildMetadata(Type type)
    {
        // AOT-SAFE: preservation comes from generated call-site rooting - JauntyAot.PreserveParameters<T>() is emitted for every parameters type the generator can see; unseen call sites get JAUNTYGEN003 at build. Spec 011.
        PropertyInfo[] props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var result = new List<ParameterMetadata>(props.Length);

        foreach (PropertyInfo p in props)
        {
            // R16: an indexer (e.g. "public object this[int i]") surfaces as a public instance
            // property named "Item" with GetIndexParameters().Length > 0. Expression.Property(cast,
            // prop) throws ArgumentException ("Incorrect number of indexes") for these, so a
            // parameters POCO that happens to declare an indexer would fail binding with an
            // unclear exception. Dapper explicitly skips indexed properties; do the same here.
            if (p.GetIndexParameters().Length > 0)
                continue;

            // AUD-R26: same defect class as the indexer above, second variant. A public set-only
            // property (no `get` accessor) makes Expression.Property throw "Expression must be
            // readable", an error naming neither Jaunty, the type nor the property. A property
            // that cannot be read cannot supply a parameter value, so it is not a parameter -
            // skipping leaves the SQL asking for it to fail as "no value found for '@Name'",
            // which at least says which one.
            if (p.GetGetMethod() is null)
                continue;

            result.Add(new ParameterMetadata(p.Name, CreateGetter(p), property: p));
        }

        return result.ToArray();
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
