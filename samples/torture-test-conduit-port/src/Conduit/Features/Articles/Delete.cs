using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Conduit.Domain;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;

using FluentValidation;

using Jaunty;
using Jaunty.Fluent;

using MediatR;

namespace Conduit.Features.Articles;

public class Delete
{
    public record Command(string Slug) : IRequest;

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() => RuleFor(x => x.Slug).NotNull().NotEmpty();
    }

    public class QueryHandler(ConduitDb db) : IRequestHandler<Command>
    {
        public async Task Handle(Command message, CancellationToken cancellationToken)
        {
            var article = await db.Connection
                .From<Article>()
                .Where(x => x.Slug == message.Slug)
                .SelectFirstOrDefaultAsync(cancellationToken)
                ?? throw new RestException(
                    HttpStatusCode.NotFound,
                    new { Article = Constants.NOT_FOUND }
                );

            // No DB-level cascade (SQLite FK enforcement not relied on) - delete children explicitly,
            // same rows EF's cascade delete configuration used to remove implicitly.
            var comments = await db.Connection
                .From<Comment>()
                .WhereIn(c => c.ArticleId, [article.ArticleId])
                .SelectAsync(cancellationToken);
            foreach (var comment in comments)
            {
                await db.Connection.DeleteAsync(comment, db.Options, cancellationToken);
            }

            var articleTags = await db.Connection
                .From<ArticleTag>()
                .WhereIn(t => t.ArticleId, [article.ArticleId])
                .SelectAsync(cancellationToken);
            foreach (var articleTag in articleTags)
            {
                await db.Connection.DeleteAsync(articleTag, db.Options, cancellationToken);
            }

            var favorites = await db.Connection
                .From<ArticleFavorite>()
                .WhereIn(f => f.ArticleId, [article.ArticleId])
                .SelectAsync(cancellationToken);
            foreach (var favorite in favorites)
            {
                await db.Connection.DeleteAsync(favorite, db.Options, cancellationToken);
            }

            await db.Connection.DeleteAsync(article, db.Options, cancellationToken);
        }
    }
}
