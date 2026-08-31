using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.Fluent.SourceGen.Tests.Entities;

[Table("sourcegen_widget_tags")]
public partial class SourceGenWidgetTag : IMapped<SourceGenWidgetTag>
{
    [Key]
    [Column("tag_id")]
    public int TagId { get; set; }

    [Column("widget_id")]
    public int WidgetId { get; set; }

    [Column("tag")]
    public string Tag { get; set; } = string.Empty;
}
