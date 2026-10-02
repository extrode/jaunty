using Extrode.Jaunty.Interfaces;
using Extrode.Jaunty.Internals;
using Extrode.Jaunty.Internals.Entity;

namespace Extrode.Jaunty.Tests.Unit.Internals;

/// <summary>
/// Tests SourceGeneratedMetadataResolver.TryBuild&lt;T&gt;'s IEntityMetadataSource probe.
/// </summary>
[Collection(ConfigurationGenerationCollection.Name)]
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

    private sealed class DefaultColumnEntity : IEntityMetadataSource
    {
        public string TableName => "t";

        public string? SchemaName => null;

        public IReadOnlyList<EntityColumnInfo> Columns =>
        [
            new EntityColumnInfo("id", "Id", true, true, false, typeof(int), _ => 1, (_, _) => { }),
            default
        ];
    }

    private sealed class UnfilledArrayEntity : IEntityMetadataSource
    {
        public string TableName => "t";

        public string? SchemaName => null;

        public IReadOnlyList<EntityColumnInfo> Columns => new EntityColumnInfo[1];
    }

    [Fact]
    public void TryBuild_ADefaultColumn_ThrowsNamingTheEntityAndIndex()
    {
        var ex = Assert.Throws<InvalidOperationException>(SourceGeneratedMetadataResolver.TryBuild<DefaultColumnEntity>);

        Assert.Contains("DefaultColumnEntity", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Columns[1]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryBuild_AnUnfilledArraySlot_Throws()
    {
        Assert.Throws<InvalidOperationException>(SourceGeneratedMetadataResolver.TryBuild<UnfilledArrayEntity>);
    }

    private sealed class ConfigurationChangingEntity : IEntityMetadataSource
    {
        public static int ColumnReads;

        public string TableName => "t" + ColumnReads;

        public string? SchemaName => null;

        public IReadOnlyList<EntityColumnInfo> Columns
        {
            get
            {
                if (++ColumnReads == 1)
                    ConfigurationGeneration.Invalidate();
                return [new EntityColumnInfo("c" + ColumnReads, "Id", true, true, false, typeof(int), _ => 1, (_, _) => { })];
            }
        }
    }

    [Fact]
    public void TryBuild_AConfigurationChangeDuringTheBuild_RebuildsFromOneConfiguration()
    {
        ConfigurationChangingEntity.ColumnReads = 0;

        EntityMetadata metadata = SourceGeneratedMetadataResolver.TryBuild<ConfigurationChangingEntity>()!;

        Assert.Equal(2, ConfigurationChangingEntity.ColumnReads);
        Assert.Equal("t2", metadata.TableName);
        Assert.Equal("c2", Assert.Single(metadata.Columns).ColumnName);
    }
}
