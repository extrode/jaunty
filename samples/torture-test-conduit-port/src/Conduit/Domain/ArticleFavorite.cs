using Jaunty.Attributes;

namespace Conduit.Domain;

[Table("ArticleFavorites")]
public class ArticleFavorite
{
    [Key]
    public int ArticleId { get; set; }

    [Ignore]
    public Article? Article { get; set; }

    [Key]
    public int PersonId { get; set; }

    [Ignore]
    public Person? Person { get; set; }
}
