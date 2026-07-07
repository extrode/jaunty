using Jaunty.Attributes;

namespace Sakila.Entities;

public class Category
{
    [Key]
    [Column("category_id")]
    public long CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
