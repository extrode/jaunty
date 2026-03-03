using Jaunty.Attributes;

namespace Jaunty.Tests.Entities;

/// <summary>
/// Category entity for SQL Server tests with PascalCase column names.
/// </summary>
[Table("Categories")]
public partial class CategorySql
{
    [Key]
    [Column("CategoryID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int CategoryId { get; set; }

    [Column("CategoryName")]
    public string CategoryName { get; set; } = string.Empty;

    [Column("Description")]
    public string? Description { get; set; }
}
