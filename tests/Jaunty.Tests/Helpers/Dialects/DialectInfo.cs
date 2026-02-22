using System;

namespace Jaunty.Tests.Helpers.Dialects;

public enum DialectProvider
{
    SystemSqlite,
    MicrosoftSqlite,
    SqlServer,
    Postgres,
    MariaDb
}

public sealed class DialectInfo
{
    public static readonly DialectInfo SystemSqlite = new("SystemSqlite", DialectProvider.SystemSqlite);
    public static readonly DialectInfo MicrosoftSqlite = new("MicrosoftSqlite", DialectProvider.MicrosoftSqlite);
    public static readonly DialectInfo SqlServer = new("SqlServer", DialectProvider.SqlServer);
    public static readonly DialectInfo Postgres = new("Postgres", DialectProvider.Postgres);
    public static readonly DialectInfo MariaDb = new("MariaDB", DialectProvider.MariaDb);

    public string Name { get; }

    public DialectProvider Provider { get; }

    public string? ConnectionString { get; }

    public DialectInfo(string name, DialectProvider provider, string? connectionString = null)
    {
        Name = name;
        Provider = provider;
        ConnectionString = connectionString;
    }

    public override string ToString() => Name;

    public string SelectTop(string selectColumns, string fromClause, int count)
    {
        return Provider == DialectProvider.SqlServer
            ? $"SELECT TOP {count} {selectColumns} {fromClause}"
            : $"SELECT {selectColumns} {fromClause} LIMIT {count}";
    }

    public Type CountReturnType => Provider == DialectProvider.SqlServer ? typeof(int) : typeof(long);

    /// <summary>
    /// Returns the Categories table SELECT clause with correct column names for this dialect.
    /// SQL Server uses PascalCase, others use snake_case.
    /// </summary>
    public string SelectCategoriesSql => Provider == DialectProvider.SqlServer
        ? "SELECT CategoryId, CategoryName, Description FROM Categories"
        : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories";

    /// <summary>
    /// Converts a SQL query with snake_case column names to the correct format for this dialect.
    /// SQL Server uses PascalCase, others use snake_case with aliases.
    /// </summary>
    public string NormalizeSql(string snakeCaseSql)
    {
        if (Provider == DialectProvider.SqlServer)
        {
            // SQL Server: Remove AS aliases and use PascalCase
            return snakeCaseSql
                .Replace("category_id AS CategoryId", "CategoryId")
                .Replace("category_name AS CategoryName", "CategoryName")
                .Replace("product_id AS ProductId", "ProductId")
                .Replace("product_name AS ProductName", "ProductName")
                .Replace("unit_price AS UnitPrice", "UnitPrice")
                .Replace("units_in_stock AS UnitsInStock", "UnitsInStock")
                .Replace("category_id", "CategoryId")
                .Replace("category_name", "CategoryName")
                .Replace("product_id", "ProductId")
                .Replace("product_name", "ProductName")
                .Replace("FROM categories", "FROM Categories")
                .Replace("FROM products", "FROM Products");
        }
        else
        {
            // PostgreSQL, MariaDB, SQLite: Keep snake_case with AS aliases (already correct)
            return snakeCaseSql;
        }
    }
}
