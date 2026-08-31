using System.Collections.Generic;
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

namespace Conduit.Features.Comments;

public class List
{
    public record Query(string Slug) : IRequest<CommentsEnvelope>;

    public class QueryHandler(ConduitDb db) : IRequestHandler<Query, CommentsEnvelope>
    {
        public async Task<CommentsEnvelope> Handle(
            Query message,
            CancellationToken cancellationToken
        )
        {
            var article = await db.Connection
                .From<Article>()
                .Where(x => x.Slug == message.Slug)
                .SelectFirstOrDefaultAsync(cancellationToken);

            if (article == null)
            {
                throw new RestException(
                    HttpStatusCode.NotFound,
                    new { Article = Constants.NOT_FOUND }
                );
            }

            var comments = await db.Connection
                .From<Comment>()
                .WhereIn(c => c.ArticleId, [article.ArticleId])
                .SelectAsync(cancellationToken);

            if (comments.Count > 0)
            {
                var authorIds = comments.Select(c => c.AuthorId).Distinct().ToList();
                var authors = await db.Connection
                    .From<Person>()
                    .WhereIn(p => p.PersonId, authorIds)
                    .SelectAsync(cancellationToken);
                var authorsById = authors.ToDictionary(p => p.PersonId);

                foreach (var comment in comments)
                {
                    comment.Author = authorsById.GetValueOrDefault(comment.AuthorId);
                }
            }

            return new CommentsEnvelope(comments);
        }
    }
}
