namespace Jaunty.Benchmarks.Entities;

[Dapper.Contrib.Extensions.Table("benchmark_products")]
public class DapperProduct
{
    [Dapper.Contrib.Extensions.Key]
    public int product_id { get; set; }
    public string product_name { get; set; } = null!;
    public decimal unit_price { get; set; }
    public int units_in_stock { get; set; }
    public bool discontinued { get; set; }
}
