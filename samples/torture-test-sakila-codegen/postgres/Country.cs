using Jaunty.Attributes;

namespace Sakila.Entities;

[Table("country", "public")]
public class Country
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("country_id")]
    public int CountryId { get; set; }

    public string Country { get; set; } = string.Empty;

    [Column("last_update")]
    public DateTimeOffset LastUpdate { get; set; }
}
