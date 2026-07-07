using Jaunty.Attributes;

namespace Sakila.Entities;

public class Address
{
    [Key]
    [Column("address_id")]
    public long AddressId { get; set; }

    public string Address { get; set; } = string.Empty;

    public string? Address2 { get; set; }

    public string District { get; set; } = string.Empty;

    [Column("city_id")]
    public long CityId { get; set; }

    [Column("postal_code")]
    public string? PostalCode { get; set; }

    public string Phone { get; set; } = string.Empty;

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
