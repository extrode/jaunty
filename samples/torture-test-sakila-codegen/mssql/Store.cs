using Jaunty.Attributes;

namespace Sakila.Entities;

[Table("store", "dbo")]
public class Store
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("store_id")]
    public int StoreId { get; set; }

    [Column("manager_staff_id")]
    public int ManagerStaffId { get; set; }

    [Column("address_id")]
    public int AddressId { get; set; }

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
