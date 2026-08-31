using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

using Conduit.Domain;
using Conduit.Infrastructure;

using Jaunty.Fluent;

namespace Conduit.Features.Articles;

/// <summary>
/// Jaunty-based replacement for the former <c>ArticleExtensions.GetAllData()</c> EF Include
/// chain. Loads the root Article row(s) via a real SQL query, then hydrates Author/ArticleTags/
/// ArticleFavorites with one targeted <c>WHERE ... IN (...)</c> query per relation (never a full
/// child-table scan or a join-induced cross product), matching the pattern established by
/// <c>samples/torture-test-eshoponweb-port</c>'s JauntyRepository.
/// </summary>
public static class ArticleQueries
{
    public static async Task<Article?> LoadArticleGraphAsync(
        ConduitDb db,
        Expression<System.Func<Article, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        var article = await db.Connection
            .From<Article>()
            .Where(predicate)
            .SelectFirstOrDefaultAsync(cancellationToken);

        if (article is null)
        {
            return null;
        }

        await HydrateAsync(db, [article], cancellationToken);
        return article;
    }

    public static async Task HydrateAsync(
        ConduitDb db,
        List<Article> articles,
        CancellationToken cancellationToken
    )
    {
        if (articles.Count == 0)
        {
            return;
        }

        var articleIds = articles.Select(a => a.ArticleId).ToList();
        var authorIds = articles.Select(a => a.AuthorId).Distinct().ToList();

        var authors = await db.Connection
            .From<Person>()
            .WhereIn(p => p.PersonId, authorIds)
            .SelectAsync(cancellationToken);
        var authorsById = authors.ToDictionary(p => p.PersonId);

        var tags = await db.Connection
            .From<ArticleTag>()
            .WhereIn(t => t.ArticleId, articleIds)
            .SelectAsync(cancellationToken);

        var favorites = await db.Connection
            .From<ArticleFavorite>()
            .WhereIn(f => f.ArticleId, articleIds)
            .SelectAsync(cancellationToken);

        foreach (var article in articles)
        {
            article.Author = authorsById.GetValueOrDefault(article.AuthorId);
            article.ArticleTags = [.. tags.Where(t => t.ArticleId == article.ArticleId)];
            article.ArticleFavorites = [.. favorites.Where(f => f.ArticleId == article.ArticleId)];
        }
    }
}
