using System.Runtime.CompilerServices;

namespace Jaunty.Internals;

/// <summary>
/// Null-checks a generic primary-key value without boxing it.
/// </summary>
/// <remarks>
/// <para>
/// AUD-R26. The typed-key families - <c>Get&lt;T, TId&gt;</c>, <c>GetRequired</c>,
/// <c>Delete&lt;T, TId&gt;</c> and their async twins - validated the key with
/// <c>ArgumentNullException.ThrowIfNull(id)</c>. That method takes <c>object?</c> and has no generic
/// overload, so every call with a value-type key - <c>int</c>, <c>long</c>, <c>Guid</c>, the
/// overwhelmingly common shape for a primary key - boxed the key purely to run a null test that can
/// never fire. The netstandard2.0 branch's <c>if (id is null)</c> boxed for the same reason. The
/// <c>object id</c> overloads on the same types are boxed by their signature anyway, so this hit
/// only the typed-key family, which exists specifically to avoid that box.
/// Constitution: "Zero unnecessary allocations in hot paths."
/// </para>
/// <para>
/// One helper rather than the guard spelled out at each of the twelve sites: the rationale is the
/// interesting part and it belongs in one place, and twelve hand-written copies of a JIT-folding
/// idiom is exactly how sites drift apart.
/// </para>
/// </remarks>
internal static class KeyGuard
{
    /// <summary>
    /// Throws <see cref="ArgumentNullException"/> when <paramref name="id"/> is
    /// <see langword="null"/>, doing nothing at all when <typeparamref name="TId"/> is a value type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>default(TId) is null</c> is a JIT-time constant per instantiation, so for a non-nullable
    /// value type the whole check - box included - is eliminated from the generated code rather than
    /// merely skipped at runtime. Where it is not folded away, the JIT elides the box for a reference
    /// type (boxing a reference is a no-op) and the null test stands.
    /// </para>
    /// <para>
    /// It is <c>default(TId) is null</c> and not <c>!typeof(TId).IsValueType</c>, which was the first
    /// attempt: <c>Nullable&lt;T&gt;</c> <em>is</em> a value type, so that form silently stopped
    /// rejecting a null <c>int?</c> key - which the old <c>ThrowIfNull</c> did reject, because boxing
    /// a null <c>Nullable&lt;int&gt;</c> produces a null reference. An <c>IEntity&lt;int?&gt;</c> is
    /// unusual but legal, and the failure mode was silence: the null would have bound as a parameter
    /// and returned no rows instead of throwing. Caught by
    /// <c>TypedKeyGuardTests.ANullNullableValueKey_StillThrows</c>.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull<TId>(TId id, string paramName)
    {
        if (default(TId) is null && id is null)
            throw new ArgumentNullException(paramName);
    }
}
