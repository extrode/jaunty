using Jaunty.Attributes;

namespace Conduit.Domain;

[Table("FollowedPeople")]
public class FollowedPeople
{
    [Key]
    public int ObserverId { get; set; }

    [Ignore]
    public Person? Observer { get; set; }

    [Key]
    public int TargetId { get; set; }

    [Ignore]
    public Person? Target { get; set; }
}
