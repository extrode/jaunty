using Jaunty.Attributes;

namespace Sakila.Entities;

[Table("film_category", "dbo")]
public class FilmCategory
{
    [Key]
    [Column("film_id")]
    public int FilmId { get; set; }

    [Key]
    [Column("category_id")]
    public int CategoryId { get; set; }

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
