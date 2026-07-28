using Jaunty.Attributes;

namespace Jaunty.SourceGenerator.Tests.Entities;

// AUD-R25 (B8-2): the generator hardcoded `public` in the partial declaration it emits, without
// reading classSymbol.DeclaredAccessibility. C# requires every partial declaration of a type to
// agree on accessibility, so an internal entity failed to compile with CS0262 ("Partial
// declarations of 'GenInternalEntity' have conflicting accessibility modifiers"). Nothing in the
// attribute docs, the generator or the scaffolder said entities had to be public, and the
// reflection path imposes no such restriction - MetadataBuilder.BuildMetadata works on any Type.
// An internal persistence model is ordinary in a library that doesn't want to expose it.
//
// This entity is the regression guard: if the generator goes back to emitting `public`, this test
// project fails to build rather than failing a test.
[Table("gen_internal_entities")]
internal partial class GenInternalEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;
}
