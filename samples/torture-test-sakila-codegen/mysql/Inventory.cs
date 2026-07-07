using Jaunty.Attributes;

namespace Sakila.Entities;

public class Inventory
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("inventory_id")]
    public int InventoryId { get; set; }

    [Column("film_id")]
    public int FilmId { get; set; }

    [Column("store_id")]
    public int StoreId { get; set; }

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
