using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities.HintCollision.NsA.Widget;

// R16: paired with NsA.Widget_Alpha - see that file for why this pairing hits the hint-name
// collision fixed in GetHintName.
[Table("gen_hint_collision_alpha")]
public partial class Alpha : IMapped<Alpha>
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
}
