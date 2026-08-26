using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// AUD-R35-220: a type-changing <c>new</c> shadow used to reach <c>ThrowIfDuplicateColumnNames</c>
/// as "'P' and 'P'". AUD-R35-221: <c>[Table("X", "")]</c> used to set the schema to the empty
/// string and discard the configured resolver.
/// </summary>
public class MetadataBuilderShadowedPropertyTests
{
    public class ShadowBase
    {
        public int Id { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    public class TypeChangingShadow : ShadowBase
    {
        public new int Value { get; set; }
    }

    public class SameSignatureShadow : ShadowBase
    {
        public new string Value { get; set; } = string.Empty;
    }

    public class MiddleShadow : ShadowBase
    {
        public new long Value { get; set; }
    }

    public class DeepestShadow : MiddleShadow
    {
        public new int Value { get; set; }
    }

    public class PlainInheritor : ShadowBase
    {
        public string Extra { get; set; } = string.Empty;
    }

    public class TwoIndexers
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public object? this[int index] { get => null; set { } }
        public object? this[string key] { get => null; set { } }
    }

    [Table("empty_schema", "")]
    public class EmptySchemaEntity
    {
        public int Id { get; set; }
    }

    [Table("real_schema", "declared")]
    public class DeclaredSchemaEntity
    {
        public int Id { get; set; }
    }

    [Fact]
    public void Build_ATypeChangingShadow_MapsOneColumnPerName()
    {
        var metadata = MetadataBuilder.Build<TypeChangingShadow>();

        Assert.Equal(2, metadata.Columns.Count);
        Assert.Single(metadata.Columns, c => c.ColumnName == "Value");
    }

    [Fact]
    public void Build_ATypeChangingShadow_KeepsTheDerivedProperty()
    {
        var metadata = MetadataBuilder.Build<TypeChangingShadow>();

        var value = metadata.Columns.First(c => c.ColumnName == "Value");

        Assert.Equal(typeof(int), value.Property.PropertyType);
        Assert.Equal(typeof(TypeChangingShadow), value.Property.DeclaringType);
    }

    [Fact]
    public void Build_ASameSignatureShadow_StillMapsOneColumn()
    {
        var metadata = MetadataBuilder.Build<SameSignatureShadow>();

        Assert.Equal(2, metadata.Columns.Count);
        Assert.Equal(typeof(string), metadata.Columns.First(c => c.ColumnName == "Value").Property.PropertyType);
    }

    [Fact]
    public void Build_AShadowShadowedAgain_KeepsTheMostDerived()
    {
        var metadata = MetadataBuilder.Build<DeepestShadow>();

        var value = metadata.Columns.First(c => c.ColumnName == "Value");

        Assert.Equal(2, metadata.Columns.Count);
        Assert.Equal(typeof(DeepestShadow), value.Property.DeclaringType);
    }

    [Fact]
    public void Build_PlainInheritance_IsUnaffected()
    {
        var metadata = MetadataBuilder.Build<PlainInheritor>();

        Assert.Equal(3, metadata.Columns.Count);
        Assert.Equal(typeof(ShadowBase), metadata.Columns.First(c => c.ColumnName == "Value").Property.DeclaringType);
    }

    [Fact]
    public void Build_TwoIndexers_AreBothSkippedAndNeitherDisplacesAColumn()
    {
        var metadata = MetadataBuilder.Build<TwoIndexers>();

        Assert.Equal(2, metadata.Columns.Count);
        Assert.DoesNotContain(metadata.Columns, c => c.ColumnName == "Item");
    }

    [Fact]
    public void Build_AnEmptySchemaAttribute_LeavesTheSchemaUnset()
    {
        var metadata = MetadataBuilder.Build<EmptySchemaEntity>();

        Assert.Null(metadata.SchemaName);
        Assert.Equal("empty_schema", metadata.TableName);
    }

    [Fact]
    public void Build_AnEmptySchemaAttribute_KeepsTheConfiguredResolverValue()
    {
        var previous = JauntyConfig.SchemaNameResolver;
        JauntyConfig.SchemaNameResolver = _ => "resolved";
        try
        {
            Assert.Equal("resolved", MetadataBuilder.Build<EmptySchemaEntity>().SchemaName);
        }
        finally
        {
            JauntyConfig.SchemaNameResolver = previous;
        }
    }

    [Fact]
    public void Build_ADeclaredSchemaAttribute_StillOverridesTheResolver()
    {
        var previous = JauntyConfig.SchemaNameResolver;
        JauntyConfig.SchemaNameResolver = _ => "resolved";
        try
        {
            Assert.Equal("declared", MetadataBuilder.Build<DeclaredSchemaEntity>().SchemaName);
        }
        finally
        {
            JauntyConfig.SchemaNameResolver = previous;
        }
    }
}
