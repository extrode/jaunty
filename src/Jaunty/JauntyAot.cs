#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Jaunty;

/// <summary>
/// Keeps a parameter object's property getters alive through a trimmed or NativeAOT publish.
/// </summary>
/// <remarks>
/// <para>
/// Spec 011. Jaunty reads a parameters object by reflecting over its public properties
/// (<c>ParameterCache</c>), and nothing in that path tells the trimmer those properties are needed.
/// On a NativeAOT publish the getters are removed and binding fails at runtime with
/// <c>No property found on type 'X' matching SQL parameter '@Id'. Available properties:</c> - the
/// list empty, because every getter is gone. Measured on <c>samples/NativeAOT-Basic</c>, 2026-07-30.
/// </para>
/// <para>
/// The methods here exist because of one measured fact: a
/// <c>[DynamicallyAccessedMembers(PublicProperties)]</c> annotation on a <em>generic type parameter</em>
/// preserves whatever type argument it is instantiated with, and it does not have to sit at the call
/// site to do it. A single <see cref="PreserveParameters{T}()"/> anywhere in reachable code is enough
/// for that type, for the whole program. Three things that look equivalent are not, and each was
/// measured:
/// </para>
/// <list type="bullet">
/// <item><description>
/// Reading the property in ordinary code does <em>not</em> preserve it. The trimmer keeps the field
/// access and drops the getter <em>method</em>, which is what reflection needs.
/// </description></item>
/// <item><description>
/// The annotation must be reached. The same calls placed in a method nobody invokes preserve
/// nothing - the trimmer removes the method and the annotation with it.
/// </description></item>
/// <item><description>
/// <c>[UnconditionalSuppressMessage]</c> preserves nothing at all; it only silences the analyzer.
/// This is the lesson spec 009 paid for.
/// </description></item>
/// </list>
/// <para>
/// <strong>Most consumers never need to call these.</strong> The Jaunty source generator inspects
/// every <c>Query</c>/<c>Execute</c> call site in the compilation, takes each parameters argument's
/// static type, and emits exactly these calls into a module initializer - so an ordinary
/// <c>connection.QueryFirst&lt;Product&gt;(sql, new { Id = 1 })</c> works after publish with no
/// source change. These methods are the escape hatch for the call sites the generator cannot see: a
/// parameters object held in an <c>object</c>-typed variable, one built by reflection, or a project
/// that references Jaunty without the analyzer. The generator reports <c>JAUNTYGEN003</c> when it
/// finds such a call site, and names <see cref="Parameters{T}(T)"/> as the fix.
/// </para>
/// <para>
/// On netstandard2.0 and net472 <c>DynamicallyAccessedMembersAttribute</c> does not exist, so these
/// compile to plain no-ops. That is correct rather than merely convenient - those targets have no
/// trimmer - and it means consumer code calling them compiles unchanged on every target.
/// </para>
/// </remarks>
/// <example>
/// A parameters object the generator cannot see, because its static type is <c>object</c>:
/// <code>
/// object p = condition ? new { Id = 1 } : new { Id = 2 };
/// var row = connection.QueryFirst&lt;Product&gt;(sql, p);          // JAUNTYGEN003
///
/// // Either wrap at the call site, where inference supplies the real type...
/// var row = connection.QueryFirst&lt;Product&gt;(sql, JauntyAot.Parameters(new { Id = 1 }));
///
/// // ...or root the type once at startup, and pass it however you like thereafter.
/// JauntyAot.PreserveParameters&lt;ProductQuery&gt;();
/// </code>
/// </example>
public static class JauntyAot
{
    /// <summary>
    /// Returns <paramref name="parameters"/> unchanged, having told the trimmer to keep the public
    /// properties of its type.
    /// </summary>
    /// <typeparam name="T">
    /// Inferred from the argument - which is the point. This is the only form that works for an
    /// anonymous type, because an anonymous type has no name to write in
    /// <see cref="PreserveParameters{T}()"/> or in <c>[DynamicDependency]</c>.
    /// </typeparam>
    /// <param name="parameters">The parameters object being passed to a Jaunty API.</param>
    /// <returns><paramref name="parameters"/>, unchanged.</returns>
    /// <remarks>
    /// Wrap the argument at the call site: <c>connection.Query&lt;Product&gt;(sql,
    /// JauntyAot.Parameters(new { Id = 1 }))</c>. There is no runtime cost - the method is an
    /// identity function and the JIT inlines it away. Its whole effect is on the type argument's
    /// annotation, which the trimmer reads at publish time.
    /// </remarks>
    public static T Parameters<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
        T>(T parameters)
        => parameters;

    /// <summary>
    /// Tells the trimmer to keep the public properties of <typeparamref name="T"/>, so instances of
    /// it can be used as Jaunty parameters objects anywhere in the program.
    /// </summary>
    /// <typeparam name="T">A named parameters type. Cannot be an anonymous type, which has no name.</typeparam>
    /// <remarks>
    /// Call once, from anywhere the program actually reaches - startup is the obvious place. The
    /// preservation is program-wide and not tied to this call's location; what matters is only that
    /// the call is not trimmed away itself, which is why it must be in reachable code.
    /// <c>[DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(T))]</c> on one
    /// of your own methods is equivalent and equally effective - measured - if you would rather not
    /// take a dependency on this API.
    /// </remarks>
    public static void PreserveParameters<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
        T>()
    {
        // Intentionally empty. The annotation on T is the entire payload.
    }

    /// <summary>
    /// Tells the trimmer to keep the public properties of the argument's type, discarding the
    /// argument itself.
    /// </summary>
    /// <typeparam name="T">Inferred from <paramref name="witness"/>.</typeparam>
    /// <param name="witness">
    /// An instance whose type is to be preserved. Never read, and never stored.
    /// </param>
    /// <remarks>
    /// The form the generator emits for anonymous types, which <see cref="PreserveParameters{T}()"/>
    /// cannot express: it constructs a throwaway instance of the same shape purely so inference has
    /// a type to bind. Anonymous type identity is structural and per-assembly, so
    /// <c>new { Id = default(int) }</c> emitted into a generated file <em>is</em> the type behind
    /// <c>new { Id = 1 }</c> written in yours - which is what lets the generated rooting reach a
    /// type nobody can name. Available to hand-write for the same reason, though
    /// <see cref="Parameters{T}(T)"/> reads better at a call site.
    /// </remarks>
    public static void PreserveParameters<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
        T>(T witness)
    {
        // Intentionally empty; see the parameterless overload.
    }
}
