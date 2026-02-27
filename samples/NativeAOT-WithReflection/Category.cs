using Jaunty.Attributes;

namespace NativeAOT.WithReflection;

[Table("categories")]
public partial class Category
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("category_id")]
    public int CategoryId { get; set; }

    [Column("category_name")]
    public string CategoryName { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;
}
