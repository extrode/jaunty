using Jaunty.Attributes;

namespace Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

[Table("inventory")]
public class InventoryItem
{
    [Key]
    public int ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public string Category { get; set; } = "";
    public int StockQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public bool InStock { get; set; }
}
