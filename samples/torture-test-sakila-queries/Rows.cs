using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace SakilaQueries;

public class RentalHistoryRow
{
    public string Title { get; set; } = string.Empty;
    public DateTime RentalDate { get; set; }
    public DateTime? ReturnDate { get; set; }
}

public class OverdueRow
{
    public int CustomerId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime RentalDate { get; set; }
}

// Spec 004 (gap #13) closure: Q02 now populates this via the fluent GroupBy-on-join API's
// Select<TResult> projection (reflection-based Activator.CreateInstance, same as every
// other LINQ-projected row type below), not raw SQL passthrough - no longer needs [Table]/
// partial/IMapped<T> (that was only ever required to trigger source-gen mapping for
// Query<T>(sql), a different, unrelated resolution path).
public class FilmRevenueRow
{
    public string Title { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class CategoryFilmCountRow
{
    public int CategoryId { get; set; }
    public int FilmCount { get; set; }
}

public class CategoryAvgRateRow
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal AvgRentalRate { get; set; }
}

public class CustomerSpendRow
{
    public int CustomerId { get; set; }
    public decimal Total { get; set; }
}

public class ActorFilmCountRow
{
    public int ActorId { get; set; }
    public int FilmCount { get; set; }
}

[Table("query_result_monthly_store_rental")]
public partial class MonthlyStoreRentalRow : IMapped<MonthlyStoreRentalRow>
{
    public int StoreId { get; set; }
    public string Month { get; set; } = string.Empty;
    public int RentalCount { get; set; }
}

public class StaffRentalCountRow
{
    public int StaffId { get; set; }
    public int RentalCount { get; set; }
}

[Table("query_result_avg")]
public partial class AvgRow : IMapped<AvgRow>
{
    public decimal Avg { get; set; }
}
