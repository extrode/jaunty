using System.Data;
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

    private static Func<IDataReader, T>? ResolveMapper()
    {
        // For NativeAOT, we check if the type implements IMapped<T>
        if (new T() is IMapped<T>)
        {
            // Optimization: Since it's a static method on the interface (or the class),
            // and we know T implements it, we can resolve it once.
            // On .NET 8+, static interface methods are ideal.
            // For now, we'll look for the static "ReadEntity" method which is what our Source Gen produces.
            var method = typeof(T).GetMethod("ReadEntity", BindingFlags.Public | BindingFlags.Static, null, [typeof(IDataReader)], null);
            if (method != null)
            {
                return (Func<IDataReader, T>)method.CreateDelegate(typeof(Func<IDataReader, T>));
            }
        }

        return null;
    }
}
