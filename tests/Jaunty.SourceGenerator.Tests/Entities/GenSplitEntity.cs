using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities;

// AUD-R25 (B8-3): Execute deduplicated its candidates with `classes.Distinct()` over
// ClassDeclarationSyntax nodes. SyntaxNode does not override Equals, so that was reference equality
// and could not collapse two *declarations* of the same type.
//
// The generator requires `partial`, which makes a [Table] entity split across files the expected
// shape rather than an exotic one. Both parts of this class passed IsSyntaxTargetForGeneration (each
// carries an attribute list) and both passed GetSemanticTargetForGeneration - GetDeclaredSymbol
// resolved both to the same INamedTypeSymbol, onto which attributes are merged, so
// HasAttribute(classSymbol, "TableAttribute") was true even for the part that does not itself carry
// [Table]. AddSource was then called twice with the identical hint name and the build failed with
// "The hint name '...JauntyMapper.g.cs' of the added source file must be unique within a generator."
//
// AUD-R25 (B8-7) later replaced that syntactic pre-filter with ForAttributeWithMetadataName, which
// only ever offers a declaration that *itself* carries the attribute - so this exact shape can no
// longer reach the dedupe at all, and this entity now proves the weaker statement that a split
// [Table] entity still generates exactly one mapper. The dedupe itself still exists, keyed on hint
// name, and is covered directly by GeneratorCachingTests.TwoPartsBothCarryingTable_EmitOnlyOneSource
// (both parts carrying [Table], which ForAttributeWithMetadataName does offer twice).
//
// Like the R16 hint-name collision entities, this is proved by compiling: a second AddSource under
// one hint name fails this test project's build outright.
[Table("gen_split_entities")]
public partial class GenSplitEntity : IMapped<GenSplitEntity>
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;
}
