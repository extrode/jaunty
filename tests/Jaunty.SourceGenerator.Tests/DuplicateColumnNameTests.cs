using Jaunty.SourceGenerator.Tests.Entities;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R18 batch-8: two properties mapped to the same column used to make the generated
/// ParameterMap dictionary initializer contain a duplicate key, throwing ArgumentException at
/// this entity's static-constructor time - the first access to any of its static members. If
/// that regressed, every assertion below would fail with a TypeInitializationException instead
/// of a normal assertion failure.
/// </summary>
public sealed class DuplicateColumnNameTests
{
    [Fact]
    public void ParameterMap_DuplicateColumn_KeepsOnlyFirstOccurrence()
    {
        Assert.Equal(2, GenDuplicateColumnEntity.ParameterMap.Count);
        Assert.True(GenDuplicateColumnEntity.ParameterMap.ContainsKey("entity_id"));
        Assert.True(GenDuplicateColumnEntity.ParameterMap.ContainsKey("code"));
        Assert.Equal("Code", GenDuplicateColumnEntity.ParameterMap["code"].PropertyName);
    }

    [Fact]
    public void EntityColumns_DuplicateColumn_StillListsBothProperties()
    {
        // EntityColumns is array-based (not keyed by column name), so it isn't affected by the
        // ParameterMap collision - both properties are still present.
        Assert.Equal(3, GenDuplicateColumnEntity.EntityColumns.Count);
        Assert.Contains(GenDuplicateColumnEntity.EntityColumns, c => c.PropertyName == "Code");
        Assert.Contains(GenDuplicateColumnEntity.EntityColumns, c => c.PropertyName == "LegacyCode");
    }
}
