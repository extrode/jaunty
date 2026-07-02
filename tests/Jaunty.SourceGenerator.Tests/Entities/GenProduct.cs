using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities;

[Table("gen_products")]
public partial class GenProduct : IMapped<GenProduct>
{
    [Key]
    [Column("product_id")]
    public int ProductId { get; set; }

    [Column("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [Column("unit_price")]
    public decimal? UnitPrice { get; set; }

    [Column("discontinued")]
    public bool Discontinued { get; set; }
}
