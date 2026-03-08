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
}