using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Interfaces;

namespace Extrode.Jaunty.Fluent.SourceGen.Tests.Entities;

[Table("crud_sourcegen_widgets")]
public partial class CrudSourceGenWidget : IMapped<CrudSourceGenWidget>
{
    [Key]
    [Column("widget_id")]
    public int WidgetId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("price")]
    public decimal Price { get; set; }
}
