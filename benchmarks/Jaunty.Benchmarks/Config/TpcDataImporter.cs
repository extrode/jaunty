using System.Data.Common;
using System.Diagnostics;

namespace Jaunty.Benchmarks.Config;

/// <summary>
/// Imports TPC-H CSV data (orders.csv, lineitem.csv) into the target database
/// using Jaunty's native CSV import API for maximum speed.
/// </summary>
public static class TpcDataImporter
{
    public static void Import(DatabaseProvider provider)
    {
        string dataDir = FindDataDirectory();

        string ordersPath = Path.Combine(dataDir, "orders.csv");
        string lineitemPath = Path.Combine(dataDir, "lineitem.csv");

        if (!File.Exists(ordersPath))
            throw new FileNotFoundException($"TPC-H orders.csv not found at: {ordersPath}");
        if (!File.Exists(lineitemPath))
            throw new FileNotFoundException($"TPC-H lineitem.csv not found at: {lineitemPath}");

        Console.WriteLine($"Importing TPC-H data for {provider}...");

        DatabaseSetup.EnsureDatabaseExists(provider);

        using var connection = DatabaseSetup.CreateConnection(provider);

        // For SQLite, use a file-based database for TPC-H data (not in-memory)
        if (provider == DatabaseProvider.Sqlite)
        {
            var sqliteConn = CreateSqliteFileConnection();
            ImportToConnection(sqliteConn, provider, ordersPath, lineitemPath);
            sqliteConn.Dispose();
            return;
        }

        connection.Open();
        ImportToConnection(connection, provider, ordersPath, lineitemPath);
    }

    private static void ImportToConnection(DbConnection connection, DatabaseProvider provider, string ordersPath, string lineitemPath)
    {
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();

        // Create schema
        DatabaseSetup.CreateTpcSchema(connection, provider);

        // Drop existing data
        Console.Write("  Clearing existing data...");
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM lineitem";
            cmd.ExecuteNonQuery();
        }
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM orders";
            cmd.ExecuteNonQuery();
        }
        Console.WriteLine(" done.");

        var options = new CsvImportOptions { HasHeader = true };

        // Import orders
        var sw = Stopwatch.StartNew();
        Console.Write($"  Importing orders.csv...");
        long orderRows = connection.ImportCsv("orders", ordersPath, options);
        sw.Stop();
        Console.WriteLine($" {orderRows:N0} rows in {sw.Elapsed.TotalSeconds:F1}s");

        // Import lineitem
        sw.Restart();
        Console.Write($"  Importing lineitem.csv...");
        long lineitemRows = connection.ImportCsv("lineitem", lineitemPath, options);
        sw.Stop();
        Console.WriteLine($" {lineitemRows:N0} rows in {sw.Elapsed.TotalSeconds:F1}s");

        Console.WriteLine($"  Total: {orderRows + lineitemRows:N0} rows imported for {provider}.");
    }

    public static DbConnection CreateSqliteFileConnection()
    {
        string dataDir = FindDataDirectory();
        string dbPath = Path.Combine(dataDir, "sqlite", "tpch.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
        return connection;
    }

    private static string FindDataDirectory()
    {
        // Walk up from current directory to find the data/ folder
        string? dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            string candidate = Path.Combine(dir, "data");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "orders.csv")))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        // Also check relative to the executable
        string exeDir = AppContext.BaseDirectory;
        dir = exeDir;
        while (dir != null)
        {
            string candidate = Path.Combine(dir, "data");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "orders.csv")))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException(
            "Could not find data/ directory containing TPC-H CSV files. " +
            "Ensure orders.csv and lineitem.csv are in the data/ directory at the project root.");
    }
}
