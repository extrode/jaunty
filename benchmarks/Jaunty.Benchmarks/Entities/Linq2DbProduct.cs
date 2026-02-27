using System.Data.Common;

using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SQLite;
using LinqToDB.Mapping;

namespace Jaunty.Benchmarks.Entities;

[Table("benchmark_products")]
public partial class Linq2DbProduct
{
    [Column("product_id"), PrimaryKey, Identity]
    public int ProductId { get; set; }

    [Column("product_name")]
    public string ProductName { get; set; } = null!;

    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    [Column("units_in_stock")]
    public int UnitsInStock { get; set; }

    [Column("discontinued")]
    public long Discontinued { get; set; }
}

public class BenchmarkDb : DataConnection
{
    public BenchmarkDb(DbConnection connection)
        : base(SQLiteTools.GetDataProvider(SQLiteProvider.Microsoft), connection, disposeConnection: false)
    {
    }

    public ITable<Linq2DbProduct> BenchmarkProducts => this.GetTable<Linq2DbProduct>();
}
