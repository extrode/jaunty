using System.Collections.Generic;
using System.Text.Json.Serialization;

using Jaunty.Attributes;

namespace Conduit.Domain;

[Table("Persons")]
public class Person
{
    [JsonIgnore]
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int PersonId { get; set; }

    public string? Username { get; set; }

    public string? Email { get; set; }

    public string? Bio { get; set; }

    public string? Image { get; set; }

    [JsonIgnore]
    [Ignore]
    public List<ArticleFavorite> ArticleFavorites { get; set; } = new();

    [JsonIgnore]
    [Ignore]
    public List<FollowedPeople> Following { get; set; } = new();

    [JsonIgnore]
    [Ignore]
    public List<FollowedPeople> Followers { get; set; } = new();

    [JsonIgnore]
    public byte[] Hash { get; set; } = [];

    [JsonIgnore]
    public byte[] Salt { get; set; } = [];
}
