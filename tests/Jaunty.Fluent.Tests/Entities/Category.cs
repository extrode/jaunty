using Jaunty.Attributes;

namespace Jaunty.Fluent.Tests.Entities;

[Table("categories")]
public class Category
{
    [Column("category_id")]
    public int CategoryId { get; set; }

    [Column("category_name")]
    public string CategoryName { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }
}
