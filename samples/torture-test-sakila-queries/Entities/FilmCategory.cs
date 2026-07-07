using Jaunty.Attributes;

namespace SakilaQueries.Entities;

[Table("film_category")]
public partial class FilmCategory
{
    [Key]
    [Column("film_id")]
    public int FilmId { get; set; }

    [Key]
    [Column("category_id")]
    public int CategoryId { get; set; }
}
