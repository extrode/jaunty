using Jaunty.Attributes;

namespace SakilaQueries.Entities;

[Table("category")]
public partial class Category
{
    [Key]
    [Column("category_id")]
    public int CategoryId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;
}
