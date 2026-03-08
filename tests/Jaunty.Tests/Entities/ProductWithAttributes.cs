using Jaunty.Attributes;

namespace Jaunty.Tests.Entities;

[Table("products")]
public partial class ProductWithAttributes
{
    [Column("product_id")]
    public long ProductId { get; set; }

    [Column("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [Column("unit_price")]
    public double? UnitPrice { get; set; }

    [Ignore]
    public string? ComputedField { get; set; }
}