namespace Jaunty.Tests.Entities;

public class Product
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public long? SupplierId { get; set; }
    public long? CategoryId { get; set; }
    public string? QuantityPerUnit { get; set; }
    public double? UnitPrice { get; set; }
    public short? UnitsInStock { get; set; }
    public short? UnitsOnOrder { get; set; }
    public short? ReorderLevel { get; set; }
    public bool Discontinued { get; set; }
}
