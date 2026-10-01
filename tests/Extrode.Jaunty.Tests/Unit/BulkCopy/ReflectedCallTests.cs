using System.Reflection;

using Extrode.Jaunty.Extensions.Reflection.BulkCopy;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.BulkCopy;

public class ReflectedCallTests
{
    private static int Thrower() => throw new InvalidOperationException("from the driver");

    private static readonly MethodInfo ThrowerMethod =
        typeof(ReflectedCallTests).GetMethod(nameof(Thrower), BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public void Unwrapped_RethrowsTheTargetsOwnException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ReflectedCall.Unwrapped(() => (int)ThrowerMethod.Invoke(null, null)!));

        Assert.Equal("from the driver", ex.Message);
        Assert.Contains(nameof(Thrower), ex.StackTrace);
    }

    [Fact]
    public void Unwrapped_ReturnsTheBodysResult()
    {
        Assert.Equal(42, ReflectedCall.Unwrapped(() => 42));
    }

    [Fact]
    public void Unwrapped_LeavesOtherExceptionsAlone()
    {
        var thrown = new ArgumentException("direct");

        var ex = Assert.Throws<ArgumentException>(() => ReflectedCall.Unwrapped<int>(() => throw thrown));

        Assert.Same(thrown, ex);
    }

    [Fact]
    public void Unwrapped_TargetInvocationWithoutAnInner_IsLeftAsIs()
    {
        var thrown = new TargetInvocationException(null);

        var ex = Assert.Throws<TargetInvocationException>(() => ReflectedCall.Unwrapped<int>(() => throw thrown));

        Assert.Same(thrown, ex);
    }

    [Fact]
    public async Task UnwrappedAsync_RethrowsTheTargetsOwnException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ReflectedCall.UnwrappedAsync(() => new ValueTask<int>((int)ThrowerMethod.Invoke(null, null)!)));

        Assert.Equal("from the driver", ex.Message);
    }

    [Fact]
    public async Task UnwrappedAsync_AfterAnAwait_RethrowsTheTargetsOwnException()
    {
        static async ValueTask<int> Body()
        {
            await Task.Yield();
            return (int)ThrowerMethod.Invoke(null, null)!;
        }

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await ReflectedCall.UnwrappedAsync(Body));
    }

    [Fact]
    public async Task UnwrappedAsync_ReturnsTheBodysResult()
    {
        Assert.Equal(7, await ReflectedCall.UnwrappedAsync(() => new ValueTask<int>(7)));
    }

    [Fact]
    public async Task UnwrappedAsync_TargetInvocationWithoutAnInner_IsLeftAsIs()
    {
        await Assert.ThrowsAsync<TargetInvocationException>(async () =>
            await ReflectedCall.UnwrappedAsync<int>(() => throw new TargetInvocationException(null)));
    }
}
