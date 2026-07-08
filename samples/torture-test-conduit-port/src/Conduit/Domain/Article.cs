using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

using Jaunty.Attributes;

namespace Conduit.Domain;

[Table("Articles")]
public class Article
{
    [JsonIgnore]
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ArticleId { get; set; }

    public string? Slug { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? Body { get; set; }

    [JsonIgnore]
    public int AuthorId { get; set; }

    [Ignore]
    public Person? Author { get; set; }

    [Ignore]
    public List<Comment> Comments { get; set; } = new();

    [Ignore]
    public bool Favorited => ArticleFavorites.Count != 0;

    [Ignore]
    public int FavoritesCount => ArticleFavorites?.Count ?? 0;

    [Ignore]
    public List<string> TagList =>
        [.. ArticleTags.Where(x => x.TagId is not null).Select(x => x.TagId!)];

    [JsonIgnore]
    [Ignore]
    public List<ArticleTag> ArticleTags { get; set; } = new();

    [JsonIgnore]
    [Ignore]
    public List<ArticleFavorite> ArticleFavorites { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
