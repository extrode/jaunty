using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities.HintCollision.NsA;

// R16: paired with NsA.Widget.Alpha (see that file) - together they prove GetHintName no longer
// collides when a literal underscore in a class name lines up with a namespace-segment dot
// elsewhere. Both used to flatten to the hint name "...NsA_Widget_Alpha", which crashed AddSource
// with a duplicate-hint-name exception.
[Table("gen_hint_collision_widget_alpha")]
public partial class Widget_Alpha : IMapped<Widget_Alpha>
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
}
