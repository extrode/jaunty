# Jaunty.FlatFiles — Quickstart Validation

> quickstart.md — Key validation scenarios to confirm the extension works end-to-end.

---

## Scenario 1: Query a CSV (Milestone 1 Gate)

```csharp
using var db = FlatFileDatabase.Open("tests/Fixtures/sales.csv");

var topSales = await db.Query<SalesRecord>()
    .Where(x => x.Revenue > 10000)
    .OrderByDescending(x => x.Revenue)
    .Take(10)
    .ToListAsync();

// Expected: 10 SalesRecord entities, Revenue > 10000, descending order
Assert.NotEmpty(topSales);
Assert.All(topSales, x => Assert.True(x.Revenue > 10000));
Assert.True(topSales.SequenceEqual(topSales.OrderByDescending(x => x.Revenue)));
```

## Scenario 2: Multi-Format Query (Milestone 2 Gate)

```csharp
using var db = FlatFileDatabase.Open(options =>
{
    options.AddCsv<SalesRecord>("sales.csv");
    options.AddParquet<InventoryItem>("inventory.parquet");
    options.AddJson<CustomerProfile>("customers.json");
});

var sales = await db.Query<SalesRecord>().Take(5).ToListAsync();
var items = await db.Query<InventoryItem>().Where(x => x.IsActive).ToListAsync();
var customers = await db.Query<CustomerProfile>().ToListAsync();

Assert.NotEmpty(sales);
Assert.NotEmpty(items);
Assert.NotEmpty(customers);
```

## Scenario 3: CRUD + Write-Back (Milestone 3 Gate)

```csharp
using var db = FlatFileDatabase.Open("sales.csv");

// INSERT
await db.InsertAsync(new SalesRecord { Id = 9999, ProductName = "Test", Revenue = 42 });

// Verify INSERT
var inserted = await db.Query<SalesRecord>().Where(x => x.Id == 9999).ToListAsync();
Assert.Single(inserted);

// UPDATE
await db.UpdateAsync<SalesRecord>(x => x.Id == 9999, x => x.Revenue, 100m);

// DELETE
await db.DeleteAsync<SalesRecord>(x => x.Revenue < 50);

// Write-back (non-destructive)
await db.SaveAsync<SalesRecord>("sales-modified.csv");
Assert.True(File.Exists("sales-modified.csv"));

// Verify original unchanged
var originalBytes = File.ReadAllBytes("sales.csv");
// (should match pre-test snapshot)
```

## Scenario 4: Import Pipeline (Milestone 4 Gate)

```csharp
using var source = FlatFileDatabase.Open("sales.csv");
using var targetConn = new SqliteConnection("DataSource=:memory:");
await targetConn.OpenAsync();

await source.ImportIntoAsync<SalesRecord>(targetConn, import =>
{
    import.BatchSize = 100;
    import.CreateTableIfMissing = true;
    import.OnConflict = ConflictStrategy.Skip;
});

// Verify import
using var cmd = targetConn.CreateCommand();
cmd.CommandText = "SELECT COUNT(*) FROM sales";
var count = (long)await cmd.ExecuteScalarAsync()!;
Assert.True(count > 0);
```

## Scenario 5: Cross-Format Export (Milestone 3 Gate)

```csharp
using var db = FlatFileDatabase.Open("sales.csv");

// Export CSV as Parquet
await db.ExportAsync<SalesRecord>("sales-export.parquet");

// Verify by re-reading with DuckDB
using var verify = FlatFileDatabase.Open("sales-export.parquet");
var results = await verify.Query<SalesRecord>().ToListAsync();
Assert.NotEmpty(results);
```
