namespace Jaunty.Tests.Entities;

public class ProductSummary
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
}