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

public class MonthlyStoreRentalRow
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

public class AvgRow
{
    public decimal Avg { get; set; }
}
