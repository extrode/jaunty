using Jaunty.Attributes;

namespace NativeAOT.Basic;

// Entity with [Table] attribute - source generator creates mappers at compile time
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