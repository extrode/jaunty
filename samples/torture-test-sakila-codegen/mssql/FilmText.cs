using Jaunty.Attributes;

namespace Sakila.Entities;

[Table("film_text", "dbo")]
public class FilmText
{
    [Key]
    [Column("film_id")]
    public int FilmId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
}
