using System.Data;
using System.Globalization;

using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;

using SakilaQueries;

Jaunty.Extensions.Reflection.JauntyReflectionExtensions.UseReflectionMapping();

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: SakilaQueries <sqlserver|postgres|mysql|mariadb|sqlite>");
    return 1;
}

var dialectArg = args[0].ToLowerInvariant();
var (dialect, connection) = dialectArg switch
{
    "sqlserver" => (Dialect.SqlServer, (IDbConnection)new SqlConnection(
        RequireEnv("JAUNTY_TORTURE_CONNSTR_MSSQL"))),
    "postgres" => (Dialect.Postgres, new NpgsqlConnection(
        RequireEnv("JAUNTY_TORTURE_CONNSTR_POSTGRES"))),
    "mysql" => (Dialect.MySql, new MySqlConnection(
        RequireEnv("JAUNTY_TORTURE_CONNSTR_MYSQL"))),
    "mariadb" => (Dialect.MariaDb, new MySqlConnection(
        RequireEnv("JAUNTY_TORTURE_CONNSTR_MARIADB"))),
    "sqlite" => (Dialect.Sqlite, new SqliteConnection(
        $"Data Source={Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "sqlite", "sakila.db")}")),
    _ => throw new ArgumentException($"Unknown dialect: {dialectArg}")
};

using (connection)
{
    connection.Open();
    var w = Console.Out;

    w.WriteLine("== Q01 RentalHistoryForCustomer(1) ==");
    foreach (var r in Queries.Q01_RentalHistoryForCustomer(connection, 1))
        w.WriteLine($"{r.Title}|{FmtDate(r.RentalDate)}|{FmtDateN(r.ReturnDate)}");

    w.WriteLine("== Q02 Top5FilmsByRevenue ==");
    foreach (var r in Queries.Q02_Top5FilmsByRevenue(connection, dialect))
        w.WriteLine($"{r.Title}|{FmtDec(r.Revenue)}");

    w.WriteLine("== Q03 ActorFilmography(1) ==");
    foreach (var title in Queries.Q03_ActorFilmography(connection, 1))
        w.WriteLine(title);

    w.WriteLine("== Q04 MonthlyRentalCountPerStore ==");
    foreach (var r in Queries.Q04_MonthlyRentalCountPerStore(connection, dialect))
        w.WriteLine($"{r.StoreId}|{r.Month}|{r.RentalCount}");

    w.WriteLine("== Q05 CustomersWithOutstandingRentals ==");
    foreach (var r in Queries.Q05_CustomersWithOutstandingRentals(connection))
        w.WriteLine($"{r.CustomerId}|{r.FirstName}|{r.LastName}|{FmtDate(r.RentalDate)}");

    w.WriteLine("== Q06 FilmCountPerCategory ==");
    foreach (var r in Queries.Q06_FilmCountPerCategory(connection).OrderBy(x => x.CategoryId))
        w.WriteLine($"{r.CategoryId}|{r.FilmCount}");

    w.WriteLine("== Q07 AverageRentalRateByCategory ==");
    foreach (var r in Queries.Q07_AverageRentalRateByCategory(connection))
        w.WriteLine($"{r.CategoryName}|{FmtDec(r.AvgRentalRate)}");

    w.WriteLine("== Q08 FilmsWithNoCategory ==");
    foreach (var f in Queries.Q08_FilmsWithNoCategory(connection))
        w.WriteLine(f.Title);

    w.WriteLine("== Q09 TopCustomersBySpend(10) ==");
    foreach (var r in Queries.Q09_TopCustomersBySpend(connection, 10))
        w.WriteLine($"{r.CustomerId}|{FmtDec(r.Total)}");

    w.WriteLine("== Q10 ActorsInManyFilms(25) ==");
    foreach (var r in Queries.Q10_ActorsInManyFilms(connection, 25).OrderBy(x => x.ActorId))
        w.WriteLine($"{r.ActorId}|{r.FilmCount}");

    w.WriteLine("== Q11 DistinctStoresForFilm(1) ==");
    foreach (var storeId in Queries.Q11_DistinctStoresForFilm(connection, 1).OrderBy(x => x))
        w.WriteLine(storeId);

    w.WriteLine("== Q12 RentalCountPerStaff ==");
    foreach (var r in Queries.Q12_RentalCountPerStaff(connection).OrderBy(x => x.StaffId))
        w.WriteLine($"{r.StaffId}|{r.RentalCount}");

    w.WriteLine("== Q13 CustomersNeverPaid ==");
    foreach (var c in Queries.Q13_CustomersNeverPaid(connection))
        w.WriteLine($"{c.CustomerId}|{c.FirstName}|{c.LastName}");

    w.WriteLine("== Q14 FilmsAboveAverageRentalRate ==");
    foreach (var f in Queries.Q14_FilmsAboveAverageRentalRate(connection))
        w.WriteLine($"{f.Title}|{FmtDec(f.RentalRate)}");

    w.WriteLine("== Q15 PagedFilmsByTitle(page 2, size 20) ==");
    foreach (var f in Queries.Q15_PagedFilmsByTitle(connection, 2, 20))
        w.WriteLine(f.Title);
}

return 0;

static string RequireEnv(string name) =>
    Environment.GetEnvironmentVariable(name)
    ?? throw new InvalidOperationException($"Environment variable {name} is not set.");

static string FmtDate(DateTime d) => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
static string FmtDateN(DateTime? d) => d.HasValue ? FmtDate(d.Value) : "NULL";
static string FmtDec(decimal d) => d.ToString("F2", CultureInfo.InvariantCulture);
