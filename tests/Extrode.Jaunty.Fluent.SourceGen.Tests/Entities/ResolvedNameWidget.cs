using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Interfaces;

namespace Extrode.Jaunty.Fluent.SourceGen.Tests.Entities;

/// <summary>
/// A source-generated entity whose table and two of whose columns are left to the
/// <c>JauntyConfig</c> naming resolvers: <c>[Table("")]</c> names no table, and only
/// <see cref="UnitPrice"/> carries a <c>[Column]</c>.
/// </summary>
[Table("")]
public partial class ResolvedNameWidget : IMapped<ResolvedNameWidget>
{
    [Key]
    public int WidgetId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    [Column("fixed_price")]
    public decimal UnitPrice { get; set; }
}
