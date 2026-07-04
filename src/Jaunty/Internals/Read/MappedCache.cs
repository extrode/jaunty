using System.Data;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;

using Jaunty.Interfaces;

namespace Jaunty.Internals.Read;

/// <summary>
/// Caches the mapper delegate for entity types that implement <see cref="IMapped{T}"/>.
/// This is the primary path for Source Generated mappers.
/// </summary>
internal static class MappedCache<T> where T : new()
{
    internal static readonly Func<IDataReader, T>? Mapper = ResolveMapper();

    /// <summary>
    /// Optional per-result-set factory (source-generated <c>CreateRowMapper</c>):
    /// validates the reader shape once and returns a closure with zero per-row
    /// validation. Preferred over <see cref="Mapper"/> by the dispatcher.
    /// </summary>
    internal static readonly Func<IDataReader, Func<IDataReader, T>>? MapperFactory = ResolveMapperFactory();

#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2090", Justification = "CreateRowMapper methods are source-generated and always preserved.")]
#endif
    private static Func<IDataReader, Func<IDataReader, T>>? ResolveMapperFactory()
    {
        if (!typeof(IMapped<T>).IsAssignableFrom(typeof(T)))
            return null;

        MethodInfo? method = typeof(T).GetMethod("CreateRowMapper", BindingFlags.Public | BindingFlags.Static, null, [typeof(IDataReader)], null);
        if (method != null && method.ReturnType == typeof(Func<IDataReader, T>))
        {
            return (Func<IDataReader, Func<IDataReader, T>>)method.CreateDelegate(typeof(Func<IDataReader, Func<IDataReader, T>>));
        }

        return null;
    }

#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2090", Justification = "ReadEntity methods are source-generated and always preserved.")]
#endif
    private static Func<IDataReader, T>? ResolveMapper()
    {
        // For NativeAOT, we check if the type implements IMapped<T>
        if (typeof(IMapped<T>).IsAssignableFrom(typeof(T)))
        {
            // On .NET 8+, source gen produces a static ReadEntity method.

            MethodInfo? method = typeof(T).GetMethod("ReadEntity", BindingFlags.Public | BindingFlags.Static, null, [typeof(IDataReader)], null);
            if (method != null)
            {
                return (Func<IDataReader, T>)method.CreateDelegate(typeof(Func<IDataReader, T>));
            }

            // Fallback for non-static ReadEntity

            MethodInfo? instanceMethod = typeof(T).GetMethod("ReadEntity", BindingFlags.Public | BindingFlags.Instance, null, [typeof(IDataReader)], null);
            if (instanceMethod != null)
            {
                return (IDataReader r) =>
                {
                    var instance = new T();
                    // We need a bridge here because instanceMethod is on IMapped<T> but we call it on T
                    if (instance is IMapped<T> mapped)
                    {
                        // On some frameworks we might need to invoke via reflection if the cast fails
                        return (T)instanceMethod.Invoke(instance, [r])!;
                    }
                    return (T)instanceMethod.Invoke(instance, [r])!;
                };
            }
        }

        return null;
    }
}