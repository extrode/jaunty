using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities;

// AUD-R25 (B8-4): the generator's property loop filtered on static/accessibility, [Ignore]/
// [NotMapped] and get-only/init-only setters, but never on IPropertySymbol.IsIndexer. An indexer
// surfaces as a public instance property named "Item" with index parameters, and C# forbids naming
// an indexer through member access - so a settable indexer was emitted as `entity.Item = ...` in
// ReadEntity/CreateRowMapper and `((GenIndexerEntity)e).Item` in the ColumnInfo/EntityColumnInfo
// lambdas, none of which compile. The reflection path degrades gracefully (the indexer is simply
// not a column); the generated path broke the build inside a .g.cs the user cannot edit.
//
// This was the third site of one hazard already fixed twice - MetadataBuilder.cs and
// ParameterCache.cs both skip indexers, the former with a comment naming this exact failure. The
// only indexer previously in the suite was a get-only one on a test helper, which the existing
// `SetMethod is null` filter already excluded, so the settable case was uncovered.
//
// Proved by compiling: without the IsIndexer guard, this file makes the test project fail to build.
[Table("gen_indexer_entities")]
public partial class GenIndexerEntity : IMapped<GenIndexerEntity>
{
    private readonly string?[] _slots = new string?[4];

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    // Settable, public, instance - passes every filter the generator had before IsIndexer.
    public string? this[int slot]
    {
        get => _slots[slot];
        set => _slots[slot] = value;
    }
}
