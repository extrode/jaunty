using Jaunty.Attributes;

namespace Sakila.Entities;

public class City
{
    [Key]
    [Column("city_id")]
    public long CityId { get; set; }

    public string City { get; set; } = string.Empty;

    [Column("country_id")]
    public long CountryId { get; set; }

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
