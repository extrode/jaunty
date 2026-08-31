using Jaunty.Attributes;

namespace Sakila.Entities;

public class City
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("city_id")]
    public int CityId { get; set; }

    public string City { get; set; } = string.Empty;

    [Column("country_id")]
    public int CountryId { get; set; }

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
