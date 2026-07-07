using Jaunty.Attributes;

namespace SakilaQueries.Entities;

[Table("actor")]
public partial class Actor
{
    [Key]
    [Column("actor_id")]
    public int ActorId { get; set; }

    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [Column("last_name")]
    public string LastName { get; set; } = string.Empty;
}
