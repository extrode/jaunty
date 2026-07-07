using Jaunty.Attributes;

namespace Sakila.Entities;

public class Inventory
{
    [Key]
    [Column("inventory_id")]
    public long InventoryId { get; set; }

    [Column("film_id")]
    public long FilmId { get; set; }

    [Column("store_id")]
    public long StoreId { get; set; }

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
