using Jaunty.Attributes;

namespace Jaunty.FlatFiles.Benchmarks;

[Table("sales")]
public class SalesRecord
{
    [Key]
    public int Id { get; set; }
    
    [Column("product_name")]
    public string ProductName { get; set; } = "";
    
    public decimal Revenue { get; set; }
    public int Quantity { get; set; }
    public DateTime Date { get; set; }
    
    [Column("region")]
    public string? Region { get; set; }
}

[Table("inventory")]
public class InventoryItem
{
    [Key]
    public int Sku { get; set; }
    public string Name { get; set; } = "";
    public int StockLevel { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsActive { get; set; }
}

[Table("customers")]
public class CustomerProfile
{
    [Key]
    public int CustomerId { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public DateTime CreatedAt { get; set; }
}
