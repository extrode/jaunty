using Jaunty.Attributes;

namespace Sakila.Entities;

public class Film
{
    [Key]
    [Column("film_id")]
    public long FilmId { get; set; }

    public string Title { get; set; } = string.Empty;

    public object? Description { get; set; }

    [Column("release_year")]
    public string? ReleaseYear { get; set; }

    [Column("language_id")]
    public long LanguageId { get; set; }

    [Column("original_language_id")]
    public long? OriginalLanguageId { get; set; }

    [Column("rental_duration")]
    public long RentalDuration { get; set; }

    [Column("rental_rate")]
    public decimal RentalRate { get; set; }

    public long? Length { get; set; }

    [Column("replacement_cost")]
    public decimal ReplacementCost { get; set; }

    public string? Rating { get; set; }

    [Column("special_features")]
    public string? SpecialFeatures { get; set; }

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
