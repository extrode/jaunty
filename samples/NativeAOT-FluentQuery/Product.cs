using Jaunty.Attributes;

namespace NativeAOT.FluentQuery;

// Entity with [Table] attribute - source generator creates mappers AND fluent metadata
// at compile time (spec 003). No Jaunty.Extensions.Reflection reference anywhere in
// this project - see the .csproj.
[Table("products")]
public partial class Product
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("product_id")]
    public int ProductId { get; set; }

    [Column("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    [Column("discontinued")]
    public bool Discontinued { get; set; }
}
