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
    public static string Top5FilmsByRevenue(Dialect dialect)
    {
        var baseQuery = @"
            SELECT f.title AS Title, SUM(p.amount) AS Revenue
            FROM payment p
            INNER JOIN rental r ON p.rental_id = r.rental_id
            INNER JOIN inventory i ON r.inventory_id = i.inventory_id
            INNER JOIN film f ON i.film_id = f.film_id
            GROUP BY f.title
            ORDER BY SUM(p.amount) DESC";

        return dialect switch
        {
            Dialect.SqlServer => baseQuery.Replace(
                "SELECT f.title AS Title, SUM(p.amount) AS Revenue",
                "SELECT TOP (5) f.title AS Title, SUM(p.amount) AS Revenue"),
            _ => baseQuery + " LIMIT 5"
        };
    }

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

    public static string AverageRentalRateByCategory() => @"
        SELECT c.name AS CategoryName, AVG(f.rental_rate) AS AvgRentalRate
        FROM film_category fc
        INNER JOIN film f ON fc.film_id = f.film_id
        INNER JOIN category c ON fc.category_id = c.category_id
        GROUP BY c.name
        ORDER BY c.name";

    public static string AverageFilmRentalRate() => "SELECT AVG(rental_rate) AS Avg FROM film";
}
