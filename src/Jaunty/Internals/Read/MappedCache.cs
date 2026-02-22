using System.Data;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;
using Jaunty.Interfaces;

namespace Jaunty.Internals;

/// <summary>
/// Caches the mapper delegate for entity types that implement <see cref="IMapped{T}"/>.
/// This is the primary path for Source Generated mappers.
/// </summary>
internal static class MappedCache<T> where T : new()
{
    internal static readonly Func<IDataReader, T>? Mapper = ResolveMapper();

#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2090", Justification = "ReadEntity methods are source-generated and always preserved.")]
#endif
    private static Func<IDataReader, T>? ResolveMapper()
    {
        // For NativeAOT, we check if the type implements IMapped<T>
        if (typeof(IMapped<T>).IsAssignableFrom(typeof(T)))
        {
            // On .NET 8+, source gen produces a static ReadEntity method.
            var method = typeof(T).GetMethod("ReadEntity", BindingFlags.Public | BindingFlags.Static, null, [typeof(IDataReader)], null);
            if (method != null)
            {
                return (Func<IDataReader, T>)method.CreateDelegate(typeof(Func<IDataReader, T>));
            }

            // Fallback for non-static ReadEntity
            var instanceMethod = typeof(T).GetMethod("ReadEntity", BindingFlags.Public | BindingFlags.Instance, null, [typeof(IDataReader)], null);
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
