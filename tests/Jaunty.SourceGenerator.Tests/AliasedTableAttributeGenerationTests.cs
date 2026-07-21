using Jaunty.SourceGenerator.Tests.Entities;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// Round 15 audit finding: proves the generator recognizes a [Table] attribute reached via a
/// using-alias (see GenAliasedTableAttributeEntity), which the old syntactic-name-matching
/// GetSemanticTargetForGeneration would have missed entirely (no generated mapper -&gt;
/// TableName/BindInsert would not exist -&gt; this file would fail to compile).
/// </summary>
public sealed class AliasedTableAttributeGenerationTests
{
    [Fact]
    public void TableName_ResolvedThroughAliasedAttribute_MatchesAttributeArgument()
    {
        Assert.Equal("gen_aliased_table_entities", GenAliasedTableAttributeEntity.TableName);
        Assert.Null(GenAliasedTableAttributeEntity.SchemaName);
    }

    [Fact]
    public void PrimaryKeyColumnNames_ResolvedThroughAliasedAttribute_ContainsKeyColumn()
    {
        Assert.Equal(["entity_id"], GenAliasedTableAttributeEntity.PrimaryKeyColumnNames);
    }
}
