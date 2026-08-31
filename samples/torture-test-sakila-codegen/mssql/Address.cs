using Jaunty.Attributes;

namespace Sakila.Entities;

[Table("address", "dbo")]
public class Address
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("address_id")]
    public int AddressId { get; set; }

    public string Address { get; set; } = string.Empty;

    public string? Address2 { get; set; }

    public string District { get; set; } = string.Empty;

    [Column("city_id")]
    public int CityId { get; set; }

    [Column("postal_code")]
    public string? PostalCode { get; set; }

    public string Phone { get; set; } = string.Empty;

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
