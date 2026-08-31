using Jaunty.Attributes;

namespace Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

// AUD-R12-127: [Table] name deliberately contains an embedded double quote to reproduce the
// ImportExecutor.ExecuteAsync<T> source-SELECT identifier-escaping regression. Same shape as
// InventoryItem so it can read the same generated CSV/Parquet fixtures.
[Table("evil\"table")]
public class EvilTableNameItem
{
    [Key]
    public int ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public string Category { get; set; } = "";
    public int StockQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public bool InStock { get; set; }
}
