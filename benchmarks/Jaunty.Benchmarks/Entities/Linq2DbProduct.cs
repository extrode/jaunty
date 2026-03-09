using System.Data.Common;

using Jaunty.Benchmarks.Config;

using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider;
using LinqToDB.DataProvider.MySql;
using LinqToDB.DataProvider.PostgreSQL;
using LinqToDB.DataProvider.SqlServer;
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
    public bool Discontinued { get; set; }
}

public class BenchmarkDb : DataConnection
{
    public BenchmarkDb(DbConnection connection, DatabaseProvider provider)
        : base(new DataOptions().UseConnection(GetDataProvider(provider, connection), connection, disposeConnection: false))
    {
    }

    public ITable<Linq2DbProduct> BenchmarkProducts => this.GetTable<Linq2DbProduct>();

    private static IDataProvider GetDataProvider(DatabaseProvider provider, DbConnection connection) => provider switch
    {
        DatabaseProvider.Sqlite => SQLiteTools.GetDataProvider(SQLiteProvider.Microsoft),
        DatabaseProvider.SqlServer => SqlServerTools.GetDataProvider(SqlServerVersion.v2017, SqlServerProvider.MicrosoftDataSqlClient),
        DatabaseProvider.PostgreSql => PostgreSQLTools.GetDataProvider(PostgreSQLVersion.v95),
        DatabaseProvider.MariaDb => MySqlTools.GetDataProvider(MySqlVersion.MariaDB10, MySqlProvider.MySqlConnector),
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };
}