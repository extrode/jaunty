using Jaunty.Attributes;

namespace SakilaQueries.Entities;

[Table("film")]
public partial class Film
{
    [Key]
    [Column("film_id")]
    public int FilmId { get; set; }

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("rental_rate")]
    public decimal RentalRate { get; set; }

    [Column("rental_duration")]
    public int RentalDuration { get; set; }
}
