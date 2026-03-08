using Jaunty.Attributes;

namespace Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

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