using Jaunty.Attributes;

namespace Jaunty.Tests.Entities;

/// <summary>
/// Test entity with composite primary key for testing composite key scenarios.
/// Maps to the order_details table in Northwind.
/// </summary>
[Table("order_details")]
public partial class OrderDetail
{
    [Key]
    [Column("order_id")]
    public int OrderId { get; set; }

    [Key]
    [Column("product_id")]
    public int ProductId { get; set; }

    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    [Column("quantity")]
    public short Quantity { get; set; }

    [Column("discount")]
    public float Discount { get; set; }
}