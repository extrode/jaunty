using RepoDb.Attributes;

namespace Jaunty.Benchmarks.Entities;

[RepoDb.Attributes.Map("benchmark_products")]
public class RepoDbProduct
{
    [Primary]
    [Identity]
    [Map("product_id")]
    public int ProductId { get; set; }

    [Map("product_name")]
    public string ProductName { get; set; } = null!;

    [Map("unit_price")]
    public decimal UnitPrice { get; set; }

    [Map("units_in_stock")]
    public int UnitsInStock { get; set; }

    [Map("discontinued")]
    public long Discontinued { get; set; }
}
