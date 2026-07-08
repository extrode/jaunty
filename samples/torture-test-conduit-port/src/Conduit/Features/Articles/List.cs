using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Conduit.Domain;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;

using Jaunty;
using Jaunty.Fluent;

using MediatR;

namespace Conduit.Features.Articles;

public class List
{
    public record Query(
        string Tag,
        string Author,
        string FavoritedUsername,
        int? Limit,
        int? Offset,
        bool IsFeed = false
    ) : IRequest<ArticlesEnvelope>;

    public class QueryHandler(ConduitDb db, ICurrentUserAccessor currentUserAccessor)
        : IRequestHandler<Query, ArticlesEnvelope>
    {
        public async Task<ArticlesEnvelope> Handle(
            Query message,
            CancellationToken cancellationToken
        )
        {
            System.Collections.Generic.List<int>? followedAuthorIds = null;
            if (message.IsFeed && currentUserAccessor.GetCurrentUsername() != null)
            {
                var currentUser = await db.Connection
                    .From<Person>()
                    .Where(x => x.Username == currentUserAccessor.GetCurrentUsername())
                    .SelectFirstOrDefaultAsync(cancellationToken);

                if (currentUser is null)
                {
                    throw new RestException(
                        HttpStatusCode.NotFound,
                        new { User = Constants.NOT_FOUND }
                    );
                }

                var following = await db.Connection
                    .From<FollowedPeople>()
                    .WhereIn(f => f.ObserverId, [currentUser.PersonId])
                    .SelectAsync(cancellationToken);

                followedAuthorIds = [.. following.Select(f => f.TargetId)];
            }

            int? favoritedByPersonId = null;
            if (!string.IsNullOrWhiteSpace(message.FavoritedUsername))
            {
                var favoritedBy = await db.Connection
                    .From<Person>()
                    .Where(x => x.Username == message.FavoritedUsername)
                    .SelectFirstOrDefaultAsync(cancellationToken);

                if (favoritedBy == null)
                {
                    return new ArticlesEnvelope();
                }

                favoritedByPersonId = favoritedBy.PersonId;
            }

            int? authorPersonId = null;
            if (!string.IsNullOrWhiteSpace(message.Author))
            {
                var author = await db.Connection
                    .From<Person>()
                    .Where(x => x.Username == message.Author)
                    .SelectFirstOrDefaultAsync(cancellationToken);

                if (author == null)
                {
                    return new ArticlesEnvelope();
                }

                authorPersonId = author.PersonId;
            }

            string? tagId = null;
            if (!string.IsNullOrWhiteSpace(message.Tag))
            {
                var tag = await db.Connection
                    .From<ArticleTag>()
                    .Where(x => x.TagId == message.Tag)
                    .SelectFirstOrDefaultAsync(cancellationToken);

                if (tag == null)
                {
                    return new ArticlesEnvelope();
                }

                tagId = tag.TagId;
            }

            var allArticles = await db.Connection
                .From<Article>()
                .OrderByDescending(x => x.CreatedAt)
                .SelectAsync(cancellationToken);

            var filtered = allArticles.AsEnumerable();

            if (followedAuthorIds is not null)
            {
                filtered = filtered.Where(x => followedAuthorIds.Contains(x.AuthorId));
            }

            if (authorPersonId is not null)
            {
                filtered = filtered.Where(x => x.AuthorId == authorPersonId);
            }

            var filteredList = filtered.ToList();

            if (tagId is not null || favoritedByPersonId is not null)
            {
                var ids = filteredList.Select(a => a.ArticleId).ToList();

                if (tagId is not null)
                {
                    var taggedIds = (await db.Connection
                        .From<ArticleTag>()
                        .WhereIn(t => t.ArticleId, ids)
                        .And(t => t.TagId == tagId)
                        .SelectAsync(cancellationToken))
                        .Select(t => t.ArticleId)
                        .ToHashSet();

                    filteredList = [.. filteredList.Where(a => taggedIds.Contains(a.ArticleId))];
                }

                if (favoritedByPersonId is not null)
                {
                    var favoritedIds = (await db.Connection
                        .From<ArticleFavorite>()
                        .WhereIn(f => f.ArticleId, filteredList.Select(a => a.ArticleId))
                        .And(f => f.PersonId == favoritedByPersonId)
                        .SelectAsync(cancellationToken))
                        .Select(f => f.ArticleId)
                        .ToHashSet();

                    filteredList = [.. filteredList.Where(a => favoritedIds.Contains(a.ArticleId))];
                }
            }

            var total = filteredList.Count;

            var page = filteredList
                .Skip(message.Offset ?? 0)
                .Take(message.Limit ?? 20)
                .ToList();

            await ArticleQueries.HydrateAsync(db, page, cancellationToken);

            return new ArticlesEnvelope { Articles = page, ArticlesCount = total };
        }
    }
}
