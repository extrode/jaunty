using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities;

// AUD-R25 (B8-3): Execute deduplicated its candidates with `classes.Distinct()` over
// ClassDeclarationSyntax nodes. SyntaxNode does not override Equals, so that was reference equality
// and could not collapse two *declarations* of the same type.
//
// The generator requires `partial`, which makes a [Table] entity split across files the expected
// shape rather than an exotic one. Both parts of this class pass IsSyntaxTargetForGeneration (each
// carries an attribute list) and both pass GetSemanticTargetForGeneration - GetDeclaredSymbol
// resolves both to the same INamedTypeSymbol, onto which attributes are merged, so
// HasAttribute(classSymbol, "TableAttribute") is true even for the part that does not itself carry
// [Table]. AddSource was then called twice with the identical hint name and the build failed with
// "The hint name '...JauntyMapper.g.cs' of the added source file must be unique within a generator."
//
// Like the R16 hint-name collision entities, this is proved by compiling: if the deduplication
// regresses to reference equality, this test project fails to build.
[Table("gen_split_entities")]
public partial class GenSplitEntity : IMapped<GenSplitEntity>
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;
}
