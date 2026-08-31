using Jaunty.Attributes;

namespace Sakila.Entities;

public class Country
{
    [Key]
    [Column("country_id")]
    public long CountryId { get; set; }

    public string Country { get; set; } = string.Empty;

    [Column("last_update")]
    public DateTime? LastUpdate { get; set; }
}
