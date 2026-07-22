using Jaunty.Internals.Entity;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// Tests SourceGeneratedMetadataResolver.TryBuild&lt;T&gt;'s IEntityMetadataSource probe.
/// </summary>
public class SourceGeneratedMetadataResolverTests
{
    private class ThrowingConstructorEntity
    {
        public ThrowingConstructorEntity() => throw new InvalidOperationException("required state not set");
    }

    // R23 batch-3: TryBuild<T> used to call new T() unconditionally to test `is
    // IEntityMetadataSource`, so a reflection-mapped POCO (which doesn't implement the
    // source-gen interface at all) with a throwing default constructor would have that
    // exception propagate instead of TryBuild gracefully returning null.
    [Fact]
    public void TryBuild_ForNonSourceGeneratedTypeWithThrowingConstructor_ReturnsNullWithoutConstructing()
    {
        EntityMetadata? result = SourceGeneratedMetadataResolver.TryBuild<ThrowingConstructorEntity>();

        Assert.Null(result);
    }
}
