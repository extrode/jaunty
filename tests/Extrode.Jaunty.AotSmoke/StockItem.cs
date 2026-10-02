using Extrode.Jaunty.Attributes;

namespace Extrode.Jaunty.AotSmoke;

[Table("")]
public partial class StockItem
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int StockItemId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    [Column("legacy_code")]
    public string Code { get; set; } = string.Empty;
}
