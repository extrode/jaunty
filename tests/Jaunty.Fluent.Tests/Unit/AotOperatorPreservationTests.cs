using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Jaunty.Fluent.Tests.Unit;

/// <summary>
/// Round-27 item 18. A Fluent predicate over a <c>decimal</c> column threw in a published NativeAOT
/// binary — <c>The binary operator GreaterThan is not defined for the types 'System.Decimal' and
/// 'System.Decimal'</c> — because <c>decimal</c>'s operator methods are never <em>called</em> by
/// <c>p =&gt; p.UnitPrice &gt; 20m</c>, only described in an expression tree that
/// <c>Expression.MakeBinary</c> resolves by reflection. The trimmer removed them.
/// </summary>
/// <remarks>
/// <para>
/// The fix is one <c>[DynamicDependency]</c> in reachable code, and these tests exist because nothing
/// else would notice its removal. Deleting it leaves every Fluent test green — they run on the JIT,
/// where no member is ever missing — and breaks every published AOT consumer using a decimal column.
/// Asserted against compiled metadata, reached by reflection because the holder is internal and adding
/// <c>InternalsVisibleTo</c> for one test is a worse trade than reading the type by name.
/// </para>
/// <para>
/// Note what is not claimed: that trimming works. Only publishing
/// <c>samples/NativeAOT-FluentQuery</c> and running it shows that, and it is the acceptance evidence
/// recorded in the registry.
/// </para>
/// </remarks>
public class AotOperatorPreservationTests
{
    private static Type OperatorRoots
        => typeof(FluentExtensions).Assembly.GetType("Jaunty.Fluent.FluentAotOperators", throwOnError: true)!;

    private static MethodInfo PreserveMethod
        => OperatorRoots.GetMethod(
            "PreserveOperatorMethods",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;

    [Fact]
    public void TheOperatorRoot_StillExists()
    {
        Assert.NotNull(PreserveMethod);
    }

    /// <summary>
    /// The measured failure was `decimal`, on every operator, plus the lifted `decimal?` forms —
    /// rooting the underlying type recovers those too, so `Nullable&lt;decimal&gt;` needs no entry.
    /// </summary>
    [Fact]
    public void DecimalsOperators_AreRooted()
    {
        IEnumerable<DynamicDependencyAttribute> dependencies =
            PreserveMethod.GetCustomAttributes<DynamicDependencyAttribute>(inherit: false);

        DynamicDependencyAttribute rooted = Assert.Single(
            dependencies,
            d => d.Type == typeof(decimal));

        Assert.Equal(DynamicallyAccessedMemberTypes.PublicMethods, rooted.MemberTypes);
    }

    /// <summary>
    /// Every entry point into the Fluent API has to reach the root, or a consumer who starts with
    /// <c>Into&lt;T&gt;</c> or <c>Cte&lt;T&gt;</c> gets the trimmed behaviour while one who starts with
    /// <c>From&lt;T&gt;</c> does not. These three are the whole surface: none of the builders has a
    /// public constructor, so there is no fourth way in.
    /// </summary>
    /// <remarks>
    /// This pins the <em>set of entry points</em>, not the calls themselves — a new
    /// <c>IDbConnection</c> extension added later fails here and has to be considered. Asserting the
    /// calls exist would mean reading IL, which is a worse trade for what it adds.
    /// </remarks>
    [Fact]
    public void TheFluentEntryPoints_AreStillTheExpectedThree()
    {
        string[] entryPoints = typeof(FluentExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetParameters().FirstOrDefault()?.ParameterType == typeof(System.Data.IDbConnection))
            .Select(m => m.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Cte", "From", "Into"], entryPoints);
    }
}
