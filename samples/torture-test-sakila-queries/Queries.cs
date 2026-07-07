using System.Data;

using Jaunty;
using Jaunty.Fluent;

using SakilaQueries.Entities;

namespace SakilaQueries;

public static class Queries
{
    public static List<RentalHistoryRow> Q01_RentalHistoryForCustomer(IDbConnection db, int customerId)
    {
        var rows = db.From<Rental>()
            .InnerJoin<Inventory>().On((r, i) => r.InventoryId == i.InventoryId)
            .InnerJoin<Film>().OnFromSecond(i => i.FilmId, f => f.FilmId)
            .Where((r, i, f) => r.CustomerId == customerId)
            .OrderByDescending(r => r.RentalDate)
            .SelectAll();

        return rows.Select(x => new RentalHistoryRow
        {
            Title = x.Item3.Title,
            RentalDate = x.Item1.RentalDate,
            ReturnDate = x.Item1.ReturnDate
        }).ToList();
    }

    public static List<FilmRevenueRow> Q02_Top5FilmsByRevenue(IDbConnection db, Dialect dialect)
    {
        return db.Query<FilmRevenueRow>(DialectSql.Top5FilmsByRevenue(dialect));
    }

    public static List<string> Q03_ActorFilmography(IDbConnection db, int actorId)
    {
        var rows = db.From<FilmActor>()
            .InnerJoin<Film>().On((fa, f) => fa.FilmId == f.FilmId)
            .Where((fa, f) => fa.ActorId == actorId)
            .OrderByJoined(f => f.Title)
            .SelectBoth();

        return rows.Select(x => x.Joined.Title).ToList();
    }

    public static List<MonthlyStoreRentalRow> Q04_MonthlyRentalCountPerStore(IDbConnection db, Dialect dialect)
    {
        return db.Query<MonthlyStoreRentalRow>(DialectSql.MonthlyRentalCountPerStore(dialect));
    }

    public static List<OverdueRow> Q05_CustomersWithOutstandingRentals(IDbConnection db)
    {
        var rows = db.From<Rental>()
            .InnerJoin<Customer>().On((r, c) => r.CustomerId == c.CustomerId)
            .Where((r, c) => r.ReturnDate == null)
            .OrderBy(r => r.RentalDate)
            .SelectBoth();

        return rows.Select(x => new OverdueRow
        {
            CustomerId = x.Joined.CustomerId,
            FirstName = x.Joined.FirstName,
            LastName = x.Joined.LastName,
            RentalDate = x.From.RentalDate
        }).ToList();
    }

    public static List<CategoryFilmCountRow> Q06_FilmCountPerCategory(IDbConnection db)
    {
        return db.From<FilmCategory>()
            .GroupBy(fc => fc.CategoryId)
            .Select(g => new CategoryFilmCountRow { CategoryId = g.Key, FilmCount = g.Count() });
    }

    public static List<CategoryAvgRateRow> Q07_AverageRentalRateByCategory(IDbConnection db)
    {
        return db.Query<CategoryAvgRateRow>(DialectSql.AverageRentalRateByCategory());
    }

    public static List<Film> Q08_FilmsWithNoCategory(IDbConnection db)
    {
        return db.From<Film>()
            .WhereNotExists<FilmCategory>((f, fc) => f.FilmId == fc.FilmId)
            .OrderBy(f => f.Title)
            .Select();
    }

    public static List<CustomerSpendRow> Q09_TopCustomersBySpend(IDbConnection db, int take)
    {
        var grouped = db.From<Payment>()
            .GroupBy(p => p.CustomerId)
            .Select(g => new CustomerSpendRow { CustomerId = g.Key, Total = g.Sum(p => p.Amount) });

        return grouped.OrderByDescending(x => x.Total).Take(take).ToList();
    }

    public static List<ActorFilmCountRow> Q10_ActorsInManyFilms(IDbConnection db, int minFilms)
    {
        return db.From<FilmActor>()
            .GroupBy(fa => fa.ActorId)
            .Having(g => g.Count() > minFilms)
            .Select(g => new ActorFilmCountRow { ActorId = g.Key, FilmCount = g.Count() });
    }

    public static List<int> Q11_DistinctStoresForFilm(IDbConnection db, int filmId)
    {
        var rows = db.From<InventoryStoreProjection>()
            .Distinct()
            .Where(i => i.FilmId == filmId)
            .Select();

        return rows.Select(x => x.StoreId).ToList();
    }

    public static List<StaffRentalCountRow> Q12_RentalCountPerStaff(IDbConnection db)
    {
        return db.From<Rental>()
            .GroupBy(r => r.StaffId)
            .Select(g => new StaffRentalCountRow { StaffId = g.Key, RentalCount = g.Count() });
    }

    public static List<Customer> Q13_CustomersNeverPaid(IDbConnection db)
    {
        return db.From<Customer>()
            .WhereNotExists<Payment>((c, p) => c.CustomerId == p.CustomerId)
            .OrderBy(c => c.CustomerId)
            .Select();
    }

    public static List<Film> Q14_FilmsAboveAverageRentalRate(IDbConnection db)
    {
        var avg = db.Query<AvgRow>(DialectSql.AverageFilmRentalRate()).First().Avg;

        return db.From<Film>()
            .Where(f => f.RentalRate > avg)
            .OrderByDescending(f => f.RentalRate)
            .Select();
    }

    public static List<Film> Q15_PagedFilmsByTitle(IDbConnection db, int page, int pageSize)
    {
        return db.From<Film>()
            .OrderBy(f => f.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select();
    }
}
