using System.Text;

using Extrode.Jaunty;
using Extrode.Jaunty.AotSmoke;
using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Fluent;

using Microsoft.Data.Sqlite;

int failures = 0;

void Check(bool condition, string what)
{
    Console.WriteLine($"{(condition ? "ok  " : "FAIL")} {what}");
    if (!condition)
        failures++;
}

static string ToSnakeCase(string name)
{
    var sb = new StringBuilder(name.Length + 4);
    for (int i = 0; i < name.Length; i++)
    {
        if (char.IsUpper(name[i]) && i > 0)
            sb.Append('_');
        sb.Append(char.ToLowerInvariant(name[i]));
    }
    return sb.ToString();
}

JauntyConfig.TableNameResolver = type => ToSnakeCase(type.Name);
JauntyConfig.ColumnNameResolver = ToSnakeCase;

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

using (SqliteCommand create = connection.CreateCommand())
{
    create.CommandText = """
        CREATE TABLE stock_item (
            stock_item_id INTEGER PRIMARY KEY AUTOINCREMENT,
            display_name TEXT NOT NULL,
            unit_price REAL NOT NULL,
            legacy_code TEXT NOT NULL
        );
        """;
    create.ExecuteNonQuery();
}

Check(StockItem.Jaunty.TableName == "stock_item", "generated TableName follows the table resolver");

long id = connection.Insert(new StockItem { DisplayName = "Bolt", UnitPrice = 1.25m, Code = "B-1" });
connection.Insert(new StockItem { DisplayName = "Nut", UnitPrice = 0.40m, Code = "N-2" });
Check(id == 1, "Insert returns the identity");

StockItem? bolt = connection.Get<StockItem>(1);
Check(bolt is { DisplayName: "Bolt", UnitPrice: 1.25m, Code: "B-1" }, "Get reads resolved and attribute-named columns");

bolt!.UnitPrice = 1.50m;
Check(connection.Update(bolt) == 1, "Update writes one row");

List<StockItem> pricey = connection.From<StockItem>().Where(s => s.UnitPrice > 1m).Select();
Check(pricey.Count == 1 && pricey[0].UnitPrice == 1.50m, "fluent Where on a resolved column");

List<StockItem> all = connection.Query<StockItem>("SELECT stock_item_id, display_name, unit_price, legacy_code FROM stock_item ORDER BY stock_item_id");
Check(all.Count == 2 && all[1].Code == "N-2", "Query maps through the generated reader");

Check(connection.Delete(bolt) == 1, "Delete removes one row");
Check(connection.From<StockItem>().Count() == 1, "one row remains");

Console.WriteLine(failures == 0 ? "AOT smoke: all checks passed" : $"AOT smoke: {failures} check(s) failed");
return failures == 0 ? 0 : 1;
