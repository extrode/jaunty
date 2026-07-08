namespace SakilaQueries;

public enum Dialect
{
    SqlServer,
    Postgres,
    MySql,
    MariaDb,
    Sqlite
}

public static class DialectSql
{
    public static string MonthlyRentalCountPerStore(Dialect dialect)
    {
        var monthExpr = dialect switch
        {
            Dialect.SqlServer => "FORMAT(r.rental_date, 'yyyy-MM')",
            Dialect.Postgres => "TO_CHAR(r.rental_date, 'YYYY-MM')",
            Dialect.MySql or Dialect.MariaDb => "DATE_FORMAT(r.rental_date, '%Y-%m')",
            Dialect.Sqlite => "strftime('%Y-%m', r.rental_date)",
            _ => throw new ArgumentOutOfRangeException(nameof(dialect))
        };

        return $@"
            SELECT i.store_id AS StoreId, {monthExpr} AS Month, COUNT(*) AS RentalCount
            FROM rental r
            INNER JOIN inventory i ON r.inventory_id = i.inventory_id
            GROUP BY i.store_id, {monthExpr}
            ORDER BY i.store_id, Month";
    }

    public static string AverageFilmRentalRate() => "SELECT AVG(rental_rate) AS Avg FROM film";
}
