using Jaunty.Attributes;

namespace Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

[Table("customers")]
public class CustomerProfile
{
    [Key]
    public int CustomerId { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public DateTime CreatedAt { get; set; }
}
