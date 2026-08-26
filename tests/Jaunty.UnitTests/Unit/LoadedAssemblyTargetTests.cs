using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;

using Xunit;

namespace Jaunty.Tests.Unit;

/// <summary>
/// Spec 010 (T13). Asserts the <c>Jaunty.dll</c> this test leg actually loaded was <em>compiled</em>
/// for the framework the leg's pin names. <see cref="Assembly.Location"/> cannot serve here: the
/// referenced dll is copied into the consuming project's output directory, so on a net10 leg the
/// path contains <c>net10.0</c> whether the net8 or net10 build was copied — green in exactly the
/// failure case this exists to catch. <see cref="TargetFrameworkAttribute"/> is stamped at compile
/// time and survives the copy.
/// </summary>
public class LoadedAssemblyTargetTests
{
    [Fact]
    public void JauntyAssembly_WasCompiledForThePinnedFramework()
    {
        string tfm = typeof(Jaunty).Assembly
            .GetCustomAttribute<TargetFrameworkAttribute>()!
            .FrameworkName;

#if NET10_0
        Assert.Equal(".NETCoreApp,Version=v10.0", tfm);
#elif NET8_0
        Assert.Equal(".NETCoreApp,Version=v8.0", tfm);
#elif NET472
        // The net472 leg pins the netstandard2.0 build.
        Assert.Equal(".NETStandard,Version=v2.0", tfm);
#else
        Assert.Fail($"Unpinned test TFM loaded Jaunty compiled as '{tfm}' - add the expected pin here.");
#endif
    }

    [Fact]
    public void AsyncEnumerableSupport_GatedMember_IsPresentInTheLoadedBuild()
    {
        // Owed by T9: ASYNC_ENUMERABLE_SUPPORT is defined for every Jaunty build (ns2.0 via
        // Bcl.AsyncInterfaces, net8+ directly), so QueryStreamAsync must exist in all of them.
        MethodInfo[] streams = typeof(Jaunty)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "QueryStreamAsync")
            .ToArray();

        Assert.NotEmpty(streams);
    }
}
