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

    /// <summary>
    /// Finds an <see cref="IMapped{T}"/> member taking an <see cref="IDataReader"/> and returning
    /// <paramref name="returnType"/>, whether it is implemented implicitly or explicitly.
    /// </summary>
    /// <remarks>
    /// AUD-R35-121. Both lookups here used to pass <see cref="BindingFlags.Public"/> alone and match
    /// on the bare name, which finds an implicit implementation and nothing else. C# permits
    /// implementing either member of <see cref="IMapped{T}"/> explicitly -
    /// <c>static T IMapped&lt;T&gt;.ReadEntity(...)</c> on net8+, or the instance member below it -
    /// and an explicit implementation is emitted as a private method named
    /// <c>Namespace.IMapped&lt;T&gt;.ReadEntity</c>. Such a type satisfies
    /// <c>typeof(IMapped&lt;T&gt;).IsAssignableFrom(typeof(T))</c>, entered the branch, found
    /// nothing, and came back with a null <c>Mapper</c> and a null <c>MapperFactory</c>: the mapper
    /// the consumer hand-wrote was silently not used and the type fell through to whatever the
    /// dispatcher does for an unmapped entity. <c>MappedCache&lt;T&gt;</c> carries no
    /// <c>where T : IMapped&lt;T&gt;</c> constraint, so it cannot reach the static abstract member
    /// through the constraint instead, and nothing in <see cref="IMapped{T}"/>'s documentation says
    /// the implementation has to be implicit. The non-public scan runs only when the public lookup
    /// misses, so the ordinary case pays nothing.
    /// </remarks>
#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2070", Justification = "Same population and the same rooting story as the GetMethod lookups this backs: a hand-written IMapped<T> the consumer roots, or JAUNTYGEN002 at build. Spec 009.")]
    [UnconditionalSuppressMessage("AOT", "IL2075", Justification = "Same population and the same rooting story as the GetMethod lookups this backs: a hand-written IMapped<T> the consumer roots, or JAUNTYGEN002 at build. Spec 009.")]
    [UnconditionalSuppressMessage("AOT", "IL2090", Justification = "Same population and the same rooting story as the GetMethod lookups this backs: a hand-written IMapped<T> the consumer roots, or JAUNTYGEN002 at build. Spec 009.")]
#endif
    private static MethodInfo? FindMappedMember(string name, BindingFlags scope, Type returnType)
    {
        // AOT-SAFE: hand-written IMapped<T> fallback only; see the suppressions above. Spec 009.
        MethodInfo? found = typeof(T).GetMethod(name, BindingFlags.Public | scope, null, [typeof(IDataReader)], null);
        if (found is not null && found.ReturnType == returnType)
            return found;

        // AOT-SAFE: same population, reached only when the public lookup missed. Spec 009.
        MethodInfo[] candidates = typeof(T).GetMethods(BindingFlags.NonPublic | scope);
        for (int i = 0; i < candidates.Length; i++)
        {
            MethodInfo candidate = candidates[i];

            // An explicit implementation's name is the interface's full name, a dot, then the
            // member name; nothing else in a type can carry a dot in its metadata name.
            int lastDot = candidate.Name.LastIndexOf('.');
            if (lastDot < 0 || string.CompareOrdinal(candidate.Name, lastDot + 1, name, 0, name.Length) != 0
                || candidate.Name.Length - lastDot - 1 != name.Length)
            {
                continue;
            }

            if (candidate.ReturnType != returnType)
                continue;

            ParameterInfo[] parameters = candidate.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(IDataReader))
                return candidate;
        }

        return null;
    }

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
        MethodInfo? method = FindMappedMember("CreateRowMapper", BindingFlags.Static, typeof(Func<IDataReader, T>));
        if (method != null)
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
            MethodInfo? method = FindMappedMember("ReadEntity", BindingFlags.Static, typeof(T));
            // AUD-R35-120: the return type used to go unchecked here while ResolveMapperFactory
            // above checks its own before calling CreateDelegate. GetMethod with an explicit
            // parameter-type array matches on parameters only, so a public static
            // ReadEntity(IDataReader) returning anything but T is found and then makes
            // CreateDelegate throw ArgumentException - out of a static field initialiser, so the
            // caller sees TypeInitializationException at whichever query touched the type first,
            // with the entity named only in the inner exception. Returning null instead lets
            // resolution fall through, which is what the factory twin already does.
            if (method != null)
            {
                // AOT-SAFE: delegate over the member resolved just above; same population and rooting.
                return (Func<IDataReader, T>)method.CreateDelegate(typeof(Func<IDataReader, T>));
            }

            // Fallback for non-static ReadEntity

            // AOT-SAFE: same population as the static lookup above - hand-written IMapped<T> with an instance ReadEntity; consumer-rooted, JAUNTYGEN002 warns. Spec 009.
            MethodInfo? instanceMethod = FindMappedMember("ReadEntity", BindingFlags.Instance, typeof(T));
            // AUD-R35-120: same return-type check as the static lookup above, for the same reason.
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
