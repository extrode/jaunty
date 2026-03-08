using Jaunty.Attributes;

namespace NativeAOT.CustomMapper;

[Table("employees")]
public partial class Employee
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("employee_id")]
    public int EmployeeId { get; set; }

    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [Column("last_name")]
    public string LastName { get; set; } = string.Empty;

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("hire_date")]
    public string HireDate { get; set; } = string.Empty;
}