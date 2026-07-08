using System;
using System.Text.Json.Serialization;

using Jaunty.Attributes;

namespace Conduit.Domain;

[Table("Comments")]
public class Comment
{
    [JsonPropertyName("id")]
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int CommentId { get; set; }

    public string? Body { get; set; }

    [Ignore]
    public Person? Author { get; set; }

    [JsonIgnore]
    public int AuthorId { get; set; }

    [JsonIgnore]
    [Ignore]
    public Article? Article { get; set; }

    [JsonIgnore]
    public int ArticleId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
