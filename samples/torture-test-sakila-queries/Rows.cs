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

// [Table] here has no real table behind it - this class is only ever populated via raw SQL
// passthrough (Query<T>(sql)), never a fluent query. The attribute exists purely to trigger
// source generation (IMapped<T>.ReadEntity), so raw-SQL DTO mapping stays reflection-free too.
[Table("query_result_film_revenue")]
public partial class FilmRevenueRow : IMapped<FilmRevenueRow>
{
    public string Title { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class CategoryFilmCountRow
{
    public int CategoryId { get; set; }
    public int FilmCount { get; set; }
}

[Table("query_result_category_avg_rate")]
public partial class CategoryAvgRateRow : IMapped<CategoryAvgRateRow>
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
