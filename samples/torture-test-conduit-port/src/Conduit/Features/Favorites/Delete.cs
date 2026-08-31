using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Conduit.Domain;
using Conduit.Features.Articles;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;

using FluentValidation;

using Jaunty;
using Jaunty.Fluent;

using MediatR;

namespace Conduit.Features.Favorites;

public class Delete
{
    public record Command(string Slug) : IRequest<ArticleEnvelope>;

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() => RuleFor(x => x.Slug).NotNull().NotEmpty();
    }

    public class QueryHandler(ConduitDb db, ICurrentUserAccessor currentUserAccessor)
        : IRequestHandler<Command, ArticleEnvelope>
    {
        public async Task<ArticleEnvelope> Handle(
            Command message,
            CancellationToken cancellationToken
        )
        {
            var article = await db.Connection
                .From<Article>()
                .Where(x => x.Slug == message.Slug)
                .SelectFirstOrDefaultAsync(cancellationToken)
                ?? throw new RestException(
                    HttpStatusCode.NotFound,
                    new { Article = Constants.NOT_FOUND }
                );

            var person = await db.Connection
                .From<Person>()
                .Where(x => x.Username == currentUserAccessor.GetCurrentUsername())
                .SelectFirstOrDefaultAsync(cancellationToken);

            if (person is null)
            {
                throw new RestException(
                    HttpStatusCode.NotFound,
                    new { Article = Constants.NOT_FOUND }
                );
            }

            var favorite = await db.Connection
                .From<ArticleFavorite>()
                .Where(x => x.ArticleId == article.ArticleId && x.PersonId == person.PersonId)
                .SelectFirstOrDefaultAsync(cancellationToken);

            if (favorite != null)
            {
                await db.Connection.DeleteAsync(favorite, db.Options, cancellationToken);
            }

            var reloaded = await ArticleQueries.LoadArticleGraphAsync(
                db,
                x => x.ArticleId == article.ArticleId,
                cancellationToken
            );

            if (reloaded is null)
            {
                throw new RestException(
                    HttpStatusCode.NotFound,
                    new { Article = Constants.NOT_FOUND }
                );
            }

            return new ArticleEnvelope(reloaded);
        }
    }
}
