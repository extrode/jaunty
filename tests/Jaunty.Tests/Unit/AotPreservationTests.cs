#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;

namespace Jaunty.Tests.Unit;

/// <summary>
/// Spec 010. <see cref="JauntyAot"/> is a set of no-op methods whose entire effect is the
/// <see cref="DynamicallyAccessedMembersAttribute"/> on their type parameters: that attribute is what
/// makes the trimmer keep a parameters object's property getters, and it is what the source generator's
/// emitted rooting relies on.
/// </summary>
/// <remarks>
/// <para>
/// These are drift tests, and they are asserted against <em>compiled metadata</em> rather than source
/// text on purpose - the review notes record why a sweep that shares the original's blind
/// spot verifies nothing. Delete the attribute and every one of these methods still compiles, every
/// other test in the suite still passes, and the published binary silently goes back to throwing
/// <c>No property found on type 'X' matching SQL parameter '@Id'. Available properties:</c> with an
/// empty list. There is no other signal. That is precisely the failure mode spec 009 was created to
/// stop happening twice, so the mechanism gets pinned where it can be seen.
/// </para>
/// <para>
/// Note what is <em>not</em> claimed here: that trimming works. Only publishing a NativeAOT binary and
/// running it shows that, which is why the spec's acceptance criterion is an executed sample.
/// </para>
/// </remarks>
public class AotPreservationTests
{
    /// <summary>
    /// The one behavioural contract the API has. Callers wrap an argument in it -
    /// <c>Query&lt;T&gt;(sql, JauntyAot.Parameters(new { Id = 1 }))</c> - so anything other than the
    /// same instance passing straight through would change what gets bound.
    /// </summary>
    [Fact]
    public void Parameters_ReturnsTheSameInstance()
    {
        var original = new { Id = 1 };

        Assert.Same(original, JauntyAot.Parameters(original));
    }

    [Fact]
    public void Parameters_PassesNullThrough()
    {
        Assert.Null(JauntyAot.Parameters<object?>(null));
    }

#if NET5_0_OR_GREATER
    /// <summary>
    /// Every generic parameter on every method here must carry the annotation. Enumerated rather than
    /// listed one by one, so a method added later is covered without anyone remembering to extend this.
    /// </summary>
    /// <remarks>
    /// Guarded because <c>DynamicallyAccessedMembersAttribute</c> does not exist below net5.0 - the
    /// reason <see cref="JauntyAot"/>'s own annotations are guarded, and the reason those targets need
    /// no rooting: they have no trimmer. Verified where it can be verified rather than not at all.
    /// </remarks>
    [Theory]
    [InlineData("Parameters")]
    [InlineData("PreserveParameters")]
    public void EveryTypeParameter_CarriesTheAnnotationThatDoesTheWork(string methodName)
    {
        MethodInfo[] overloads = typeof(JauntyAot)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == methodName)
            .ToArray();

        Assert.NotEmpty(overloads);

        foreach (MethodInfo overload in overloads)
        {
            Type typeParameter = Assert.Single(overload.GetGenericArguments());

            DynamicallyAccessedMembersAttribute? annotation = typeParameter
                .GetCustomAttributes<DynamicallyAccessedMembersAttribute>(inherit: false)
                .SingleOrDefault();

            Assert.NotNull(annotation);
            Assert.Equal(DynamicallyAccessedMemberTypes.PublicProperties, annotation!.MemberTypes);
        }
    }
#endif

    /// <summary>
    /// The generator emits <c>PreserveParameters(witness)</c> for anonymous types, which have no name to
    /// write as a type argument, and <c>PreserveParameters&lt;T&gt;()</c> for everything else. Both
    /// forms have to exist or the emitted file does not compile.
    /// </summary>
    [Fact]
    public void PreserveParameters_HasBothTheNamedAndTheWitnessForm()
    {
        MethodInfo[] overloads = typeof(JauntyAot)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "PreserveParameters")
            .ToArray();

        Assert.Equal(2, overloads.Length);
        Assert.Single(overloads, m => m.GetParameters().Length == 0);
        Assert.Single(overloads, m => m.GetParameters().Length == 1);
    }

    /// <summary>
    /// Calling them must be free of side effects - the generated module initializer runs these at
    /// startup in every consumer assembly, on the JIT as well as after a trimmed publish.
    /// </summary>
    [Fact]
    public void PreserveParameters_DoesNothingAtRuntime()
    {
        JauntyAot.PreserveParameters<AotPreservationTests>();
        JauntyAot.PreserveParameters(new { Id = default(int) });
    }
}
