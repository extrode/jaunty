using Jaunty.Attributes;

namespace SakilaQueries.Entities;

[Table("inventory")]
public partial class Inventory
{
    [Key]
    [Column("inventory_id")]
    public int InventoryId { get; set; }

    [Column("film_id")]
    public int FilmId { get; set; }

    [Column("store_id")]
    public int StoreId { get; set; }
}
