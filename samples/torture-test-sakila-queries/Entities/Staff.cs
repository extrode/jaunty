using Jaunty.Attributes;

namespace SakilaQueries.Entities;

[Table("staff")]
public partial class Staff
{
    [Key]
    [Column("staff_id")]
    public int StaffId { get; set; }

    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [Column("last_name")]
    public string LastName { get; set; } = string.Empty;

    [Column("store_id")]
    public int StoreId { get; set; }
}
