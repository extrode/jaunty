#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;

namespace Jaunty.Fluent;

/// <summary>
/// Keeps the operator methods that <see cref="System.Linq.Expressions.Expression"/> looks up by
/// reflection alive through a trimmed or NativeAOT publish.
/// </summary>
/// <remarks>
/// <para>
/// Round-27 item 18. <c>samples/NativeAOT-FluentQuery</c>, published with <c>PublishAot=true</c>,
/// threw before reaching any Jaunty code:
/// </para>
/// <code>
/// InvalidOperationException: The binary operator GreaterThan is not defined for the types
/// 'System.Decimal' and 'System.Decimal'.
///   at System.Linq.Expressions.Expression.GetUserDefinedBinaryOperatorOrThrow(...)
///   at Program.&lt;Main&gt;$(String[] args)
/// </code>
/// <para>
/// <c>decimal</c>'s comparison and arithmetic operators are ordinary static methods
/// (<c>op_GreaterThan</c> and friends), and a predicate like <c>p =&gt; p.UnitPrice &gt; 20m</c> never
/// <em>calls</em> them - it describes them in an expression tree, which <c>Expression.MakeBinary</c>
/// then resolves by reflection. Nothing in the program references the methods, so the trimmer removes
/// them, and building the tree fails at runtime.
/// </para>
/// <para>
/// Measured, 2026-07-30, in a published binary: <b>every</b> decimal operator failed - <c>&gt;</c>,
/// <c>&lt;</c>, <c>&gt;=</c>, <c>==</c>, <c>!=</c>, <c>+</c>, <c>*</c> - along with the lifted
/// <c>decimal?</c> forms. Rooting the underlying type fixes the lifted forms too, so
/// <c>Nullable&lt;decimal&gt;</c> needs no entry of its own. <c>int</c>, <c>double</c>,
/// <c>string</c> and <c>bool</c> never fail: their operators are intrinsic to the expression API and
/// involve no lookup.
/// </para>
/// <para>
/// <b>Why this is Jaunty.Fluent's problem even though the throw is in consumer code.</b> The whole
/// Fluent surface is expression trees - <c>Where</c>, <c>OrderBy</c>, <c>Select</c> projections - and
/// the library ships a NativeAOT sample asserting they work. A consumer cannot reasonably be expected
/// to know that comparing one of the most ordinary column types requires a trimmer directive. Jaunty
/// asks for the expression tree, so Jaunty arranges for it to be constructible; the same reasoning as
/// spec 010, where Jaunty reflects over parameter objects and so arranges their preservation.
/// </para>
/// <para>
/// <b>Why it is called explicitly from the three entry points.</b> The directive has to sit in code the
/// trimmer actually keeps - spec 010 measured the alternative, and annotations inside a method nobody
/// calls are removed along with it. A <c>[ModuleInitializer]</c> would guarantee that unconditionally
/// and was the first attempt, but <c>CA2255</c> rejects one in a library, correctly. So the reachability
/// comes from an ordinary call in each of <see cref="FluentExtensions.From{T}"/>,
/// <see cref="FluentExtensions.Into{T}"/> and <see cref="FluentExtensions.Cte{T}"/> - which is
/// exhaustive, because those are the only three entry points into the Fluent API and none of the
/// builders has a public constructor. The method is empty, so the JIT inlines the call to nothing.
/// </para>
/// <para>
/// Guarded to net5.0 and later because <see cref="DynamicDependencyAttribute"/> does not exist below
/// it - and those targets have no trimmer, so there is nothing to preserve against.
/// </para>
/// </remarks>
internal static class FluentAotOperators
{
    /// <summary>
    /// Roots <see cref="decimal"/>'s operator methods, which <c>Expression.MakeBinary</c> resolves by
    /// reflection when a predicate over a decimal column is built.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only <c>decimal</c> is listed, and that was a decision rather than an oversight.</b>
    /// <see cref="DateTime"/>, <see cref="DateTimeOffset"/>, <see cref="TimeSpan"/> and
    /// <see cref="Guid"/> have the identical shape - user-defined operator methods reached only by
    /// reflection from an expression tree - and all four were measured <em>passing</em> in the same
    /// published binary, apparently because something else in the application referenced them.
    /// </para>
    /// <para>
    /// Adding them anyway was implemented and then reverted, on the numbers. Published sample size:
    /// 6,116,472 bytes with no rooting (and broken), 6,182,824 with <c>decimal</c> alone
    /// (+66 KB, +1.09%, and working), 6,315,640 with all five (+199 KB, +3.26%). So the four
    /// precautionary entries cost 133 KB - 2.17% of every consumer's binary - to defend against a
    /// failure that has never been observed. Spec 009 declined to add <c>IdSetter</c> to a public
    /// interface on the same reasoning: not on an unmeasured premise.
    /// </para>
    /// <para>
    /// What is genuinely unknown is whether a leaner application loses those four. If one ever does, the
    /// symptom will be this same exception with a different type name, and the fix is one line here.
    /// Recorded as an open question in round-27 item 18 rather than pre-empted.
    /// </para>
    /// </remarks>
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(decimal))]
    internal static void PreserveOperatorMethods()
    {
        // Intentionally empty; the attributes above are the entire payload.
    }
}
#endif
