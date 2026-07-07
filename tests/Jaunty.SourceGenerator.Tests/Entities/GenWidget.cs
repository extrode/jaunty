using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities;

[Table("gen_widgets", "dbo")]
public partial class GenWidget : IMapped<GenWidget>
{
    [Key]
    [Column("widget_id")]
    public int WidgetId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;
}
