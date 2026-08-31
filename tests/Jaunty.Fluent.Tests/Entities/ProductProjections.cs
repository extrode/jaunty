using Jaunty.Attributes;

namespace Jaunty.Fluent.Tests.Entities;

/// <summary>
/// Simple DTO for testing Select&lt;T&gt; with custom entities.
/// Maps a subset of Product columns.
/// </summary>
public class ProductInfo
{
    [Column("product_id")]
    public int ProductId { get; set; }

    [Column("product_name")]
    public string ProductName { get; set; } = null!;

    [Column("unit_price")]
    public decimal? UnitPrice { get; set; }
}

/// <summary>
/// DTO for testing custom mapper with columns from both Product and Category.
/// </summary>
public class ProductCategoryDto
{
    public string ProductName { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
}