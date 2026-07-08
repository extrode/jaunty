using Jaunty.Attributes;

namespace Conduit.Domain;

[Table("ArticleTags")]
public class ArticleTag
{
    [Key]
    public int ArticleId { get; set; }

    [Ignore]
    public Article? Article { get; set; }

    [Key]
    public string? TagId { get; set; }

    [Ignore]
    public Tag? Tag { get; set; }
}
