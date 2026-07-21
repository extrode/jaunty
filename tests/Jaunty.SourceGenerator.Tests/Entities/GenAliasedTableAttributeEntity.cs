using JauntyTable = Jaunty.Attributes.TableAttribute;

using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities;

// Round 15 audit finding: GetSemanticTargetForGeneration used to match the attribute by
// syntactic name (attribute.Name.ToString() against 4 hardcoded spellings), so a using-alias
// like this one - which never appears as any of those literal strings - would have silently
// produced no generated mapper at all. Now resolved via the semantic model
// (HasAttribute(classSymbol, "TableAttribute")), which sees through the alias to the real
// attribute class. This file existing and its entity being generatable is itself part of the
// coverage - if generation failed, GenAliasedTableAttributeEntityTests would not compile.
[JauntyTable("gen_aliased_table_entities")]
public partial class GenAliasedTableAttributeEntity : IMapped<GenAliasedTableAttributeEntity>
{
    [Key]
    [Column("entity_id")]
    public int EntityId { get; set; }

    [Column("label")]
    public string Label { get; set; } = string.Empty;
}
