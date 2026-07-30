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
/// <remarks>
/// Spec 009. Resolution goes through <see cref="IGeneratedAccessors{T}"/> first, which hands over the
/// generated mapper as a delegate. The reflection below is now only reached by types the generator
/// did not produce - a hand-written <see cref="IMapped{T}"/>, or a <c>[Table]</c> entity compiled
/// against a Jaunty old enough to predate the interface.
/// </remarks>
internal static class MappedCache<T> where T : new()
{
    /// <summary>
    /// <typeparamref name="T"/>'s source-generated accessors, or <see langword="null"/> when
    /// <typeparamref name="T"/> is not source-generated.
    /// </summary>
    /// <remarks>
    /// Declared first on purpose: static field initialisers run in textual order, and both fields
    /// below read this one. Resolved once per closed generic rather than once per accessor, so the
    /// <c>new T()</c> the cast needs is paid a single time - the same cost
    /// <c>WriteParameterCache&lt;T&gt;.ResolveMetadata</c> already pays.
    /// </remarks>
    private static readonly IGeneratedAccessors<T>? Accessors = ResolveAccessors();

    internal static readonly Func<IDataReader, T>? Mapper = ResolveMapper();

    /// <summary>
    /// Optional per-result-set factory (source-generated <c>CreateRowMapper</c>):
    /// validates the reader shape once and returns a closure with zero per-row
    /// validation. Preferred over <see cref="Mapper"/> by the dispatcher.
    /// </summary>
    internal static readonly Func<IDataReader, Func<IDataReader, T>>? MapperFactory = ResolveMapperFactory();

    /// <summary>
    /// Casts <typeparamref name="T"/> to its generated accessors. No reflection over members: the
    /// interface check and the cast are all that is needed, mirroring
    /// <c>SourceGeneratedMetadataResolver.TryBuild&lt;T&gt;</c>.
    /// </summary>
    private static IGeneratedAccessors<T>? ResolveAccessors()
        => typeof(IGeneratedAccessors<T>).IsAssignableFrom(typeof(T))
            ? (IGeneratedAccessors<T>)new T()
            : null;

#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2090", Justification = "Reached only when T is not source-generated - a source-generated T implements IGeneratedAccessors<T> and returns above, with no reflection. The remaining population is a hand-written IMapped<T>, whose CreateRowMapper this cannot arrange to preserve: the consumer must root it (for example with [DynamicDependency]) or implement IGeneratedAccessors<T>. The generator reports JAUNTYGEN002 for exactly this case, so it is a build-time warning rather than a trimmed-away method discovered at runtime. Spec 009.")]
#endif
    private static Func<IDataReader, Func<IDataReader, T>>? ResolveMapperFactory()
    {
        // Source-generated: the delegate is handed over directly, so the generated CreateRowMapper
        // is statically referenced and survives trimming.
        if (Accessors is not null)
            return Accessors.RowMapperFactory;

        if (!typeof(IMapped<T>).IsAssignableFrom(typeof(T)))
            return null;

        // AOT-SAFE: hand-written IMapped<T> fallback only; a source-generated T returned above via Accessors. The consumer roots CreateRowMapper or implements IGeneratedAccessors<T>; JAUNTYGEN002 warns at build. Spec 009.
        MethodInfo? method = typeof(T).GetMethod("CreateRowMapper", BindingFlags.Public | BindingFlags.Static, null, [typeof(IDataReader)], null);
        if (method != null && method.ReturnType == typeof(Func<IDataReader, T>))
        {
            // AOT-SAFE: delegate over the member resolved just above; same population and rooting.
            return (Func<IDataReader, Func<IDataReader, T>>)method.CreateDelegate(typeof(Func<IDataReader, Func<IDataReader, T>>));
        }

        return null;
    }

#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2090", Justification = "Reached only when T is not source-generated - a source-generated T implements IGeneratedAccessors<T> and returns above, with no reflection. The remaining population is a hand-written IMapped<T>, whose ReadEntity this cannot arrange to preserve: the consumer must root it (for example with [DynamicDependency]) or implement IGeneratedAccessors<T>. The generator reports JAUNTYGEN002 for exactly this case, so it is a build-time warning rather than a trimmed-away method discovered at runtime. Spec 009.")]
#endif
    private static Func<IDataReader, T>? ResolveMapper()
    {
        // Source-generated: see ResolveMapperFactory. Below net8.0 the accessor's delegate
        // constructs a fresh T per row, matching the instance fallback further down.
        if (Accessors is not null)
            return Accessors.RowMapper;

        // For NativeAOT, we check if the type implements IMapped<T>
        if (typeof(IMapped<T>).IsAssignableFrom(typeof(T)))
        {
            // On .NET 8+, source gen produces a static ReadEntity method.

            // AOT-SAFE: hand-written IMapped<T> fallback only; a source-generated T returned above via Accessors. The consumer roots ReadEntity or implements IGeneratedAccessors<T>; JAUNTYGEN002 warns at build. Spec 009.
            MethodInfo? method = typeof(T).GetMethod("ReadEntity", BindingFlags.Public | BindingFlags.Static, null, [typeof(IDataReader)], null);
            if (method != null)
            {
                // AOT-SAFE: delegate over the member resolved just above; same population and rooting.
                return (Func<IDataReader, T>)method.CreateDelegate(typeof(Func<IDataReader, T>));
            }

            // Fallback for non-static ReadEntity

            // AOT-SAFE: same population as the static lookup above - hand-written IMapped<T> with an instance ReadEntity; consumer-rooted, JAUNTYGEN002 warns. Spec 009.
            MethodInfo? instanceMethod = typeof(T).GetMethod("ReadEntity", BindingFlags.Public | BindingFlags.Instance, null, [typeof(IDataReader)], null);
            if (instanceMethod != null)
            {
                // AOT-SAFE: delegate over the member resolved just above; same population and rooting.
                var openDelegate = (Func<T, IDataReader, T>)instanceMethod.CreateDelegate(typeof(Func<T, IDataReader, T>));
                return (IDataReader r) => openDelegate(new T(), r);
            }
        }

        return null;
    }
}
