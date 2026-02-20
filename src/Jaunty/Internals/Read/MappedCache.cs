using System.Data;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;

using Jaunty.Interfaces;

namespace Jaunty.Internals;

/// <summary>
/// Caches the mapper delegate for entity types that implement <see cref="IMapped{T}"/>.
/// </summary>
/// <typeparam name="T">The entity type to map.</typeparam>
/// <remarks>
/// <para>
/// This cache stores a compiled delegate that maps <see cref="IDataReader"/> rows to entity instances.
/// The mapper is resolved once per type and reused for all subsequent read operations.
/// </para>
/// <para>
/// Only entity types that implement <see cref="IMapped{T}"/> will have a mapper resolved.
/// For other types, <see cref="Mapper"/> will be <see langword="null"/>.
/// </para>
/// </remarks>
internal static class MappedCache<
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] 
#endif
    T> where T : new()
{
    /// <summary>
    /// The cached mapper delegate for type <typeparamref name="T"/>, or <see langword="null"/> if the type does not implement <see cref="IMapped{T}"/>.
    /// </summary>
    internal static readonly Func<IDataReader, T>? Mapper = ResolveMapper();

    /// <summary>
    /// Resolves the mapper delegate by finding the static <c>ReadEntity</c> method on type <typeparamref name="T"/>.
    /// </summary>
    /// <returns>The mapper delegate, or <see langword="null"/> if no valid mapper method was found.</returns>
    /// <remarks>
    /// <para>
    /// On .NET 8+, uses <see cref="MethodInfo.CreateDelegate"/> for optimal performance.
    /// On earlier frameworks, uses reflection invocation with a wrapper delegate.
    /// </para>
    /// </remarks>
    internal static Func<IDataReader, T>? ResolveMapper()
    {
        Type entityType = typeof(T);
        bool implementsIMapped = false;
        foreach (var iface in entityType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IMapped<>))
            {
                implementsIMapped = true;
                break;
            }
        }

        if (!implementsIMapped)
            return null;

#if NET8_0_OR_GREATER
        var readMethod = entityType.GetMethod("ReadEntity", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(IDataReader) }, null);

        if (readMethod is null)
            return null;

        return readMethod.CreateDelegate<Func<IDataReader, T>>();
#else
        var readMethod = entityType.GetMethod("ReadEntity", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(IDataReader) }, null);

        if (readMethod is null)
            return null;

        return reader =>
        {
            var instance = new T();
            return (T)readMethod.Invoke(instance, new object[] { reader });
        };
#endif
    }
}
