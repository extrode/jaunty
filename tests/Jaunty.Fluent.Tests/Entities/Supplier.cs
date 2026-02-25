using Jaunty.Attributes;

namespace Jaunty.Fluent.Tests.Entities;

[Table("suppliers")]
public class Supplier
{
    [Column("supplier_id")]
    public int SupplierId { get; set; }

    [Column("company_name")]
    public string CompanyName { get; set; } = null!;

    [Column("contact_name")]
    public string? ContactName { get; set; }

    [Column("city")]
    public string? City { get; set; }

    [Column("country")]
    public string? Country { get; set; }
}
