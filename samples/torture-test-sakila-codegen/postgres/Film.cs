using Jaunty.Attributes;

namespace Sakila.Entities;

[Table("film", "public")]
public class Film
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("film_id")]
    public int FilmId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Column("release_year")]
    public int? ReleaseYear { get; set; }

    [Column("language_id")]
    public int LanguageId { get; set; }

    [Column("original_language_id")]
    public int? OriginalLanguageId { get; set; }

    [Column("rental_duration")]
    public short RentalDuration { get; set; }

    [Column("rental_rate")]
    public decimal RentalRate { get; set; }

    public short? Length { get; set; }

    [Column("replacement_cost")]
    public decimal ReplacementCost { get; set; }

    public object? Rating { get; set; }

    [Column("last_update")]
    public DateTimeOffset LastUpdate { get; set; }

    [Column("special_features")]
    public object? SpecialFeatures { get; set; }

    public object Fulltext { get; set; } = null!;
}
