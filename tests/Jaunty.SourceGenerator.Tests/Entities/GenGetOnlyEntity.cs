using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities;

// AUD-R14: a get-only or init-only property that isn't marked [Ignore]/[NotMapped] used to make
// the generator emit `entity.Prop = ...` assignments for it anyway, which is a compile error
// (CS0200/CS8852) for both kinds of property. Code (init-only) and Display (get-only, computed)
// prove the generator now treats both as implicitly ignored instead of failing to compile.
[Table("gen_getonly_entities")]
public partial class GenGetOnlyEntity : IMapped<GenGetOnlyEntity>
{
    [Key]
    [Column("entity_id")]
    public int EntityId { get; set; }

    [Column("code")]
    public string Code { get; init; } = string.Empty;

    [Column("label")]
    public string Label { get; set; } = string.Empty;

    public string Display => $"{EntityId}:{Label}";
}
