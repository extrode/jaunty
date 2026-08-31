using Jaunty.Attributes;

namespace Sakila.Entities;

[Table("film_category")]
public class FilmCategory
{
    [Key]
    [Column("film_id")]
    public long FilmId { get; set; }

    [Key]
    [Column("category_id")]
    public long CategoryId { get; set; }

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
