using Jaunty.Attributes;

namespace Sakila.Entities;

[Table("film_text")]
public class FilmText
{
    [Key]
    [Column("film_id")]
    public long FilmId { get; set; }

    public string Title { get; set; } = string.Empty;

    public object? Description { get; set; }
}
