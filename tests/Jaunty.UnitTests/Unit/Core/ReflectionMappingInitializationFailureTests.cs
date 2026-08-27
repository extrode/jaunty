using System.Reflection;

using Xunit;

namespace Jaunty.Tests.Unit.Core;

/// <summary>
/// AUD-R35-152. The catch-all that AUD-R26-055 added had no test: the existing
/// <see cref="ReflectionMappingInitializationTests"/> asserts only that a healthy process records no
/// error, which stays true if the recording is reverted to a bare <c>catch { }</c>. The assembly
/// load is now a parameter and the swallowed exception a return value, so both arms can be driven
/// without leaving a recorded error behind for the rest of the process.
/// </summary>
public class ReflectionMappingInitializationFailureTests
{
    [Fact]
    public void AnUnexpectedFailure_IsHandedBackRatherThanSwallowed()
    {
        var boom = new InvalidOperationException("UseReflectionMapping blew up");

        Exception? recorded = Jaunty.TryEnableReflectionMapping(_ => throw boom);

        Assert.Same(boom, recorded);
    }

    [Theory]
    [InlineData(typeof(FileNotFoundException))]
    [InlineData(typeof(TypeLoadException))]
    [InlineData(typeof(MissingMethodException))]
    public void TheSupportedAbsentAssemblyCases_AreNotFailures(Type exceptionType)
    {
        var expected = (Exception)Activator.CreateInstance(exceptionType)!;

        Exception? recorded = Jaunty.TryEnableReflectionMapping(_ => throw expected);

        Assert.Null(recorded);
    }

    [Fact]
    public void TheRealLoad_RecordsNothing()
    {
        Exception? recorded = Jaunty.TryEnableReflectionMapping(Assembly.Load);

        Assert.Null(recorded);
    }

    [Fact]
    public void DrivingTheSeam_LeavesTheRecordedErrorAlone()
    {
        Jaunty.TryEnableReflectionMapping(_ => throw new InvalidOperationException("boom"));

        Assert.Null(Jaunty.ReflectionMappingInitializationError);
    }

    [Fact]
    public void AnAssemblyWithoutTheExtensionType_IsNotAFailure()
    {
        Exception? recorded = Jaunty.TryEnableReflectionMapping(_ => typeof(string).Assembly);

        Assert.Null(recorded);
    }
}
