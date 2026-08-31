using Jaunty.Attributes;

namespace SakilaQueries.Entities;

[Table("inventory")]
public partial class InventoryStoreProjection
{
    [Column("store_id")]
    public int StoreId { get; set; }

    [Column("film_id")]
    public int FilmId { get; set; }
}
