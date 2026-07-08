using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;

using FluentValidation;

using MediatR;

namespace Conduit.Features.Articles;

public class Details
{
    public record Query(string Slug) : IRequest<ArticleEnvelope>;

    public class QueryValidator : AbstractValidator<Query>
    {
        public QueryValidator() => RuleFor(x => x.Slug).NotNull().NotEmpty();
    }

    public class QueryHandler(ConduitDb db) : IRequestHandler<Query, ArticleEnvelope>
    {
        public async Task<ArticleEnvelope> Handle(
            Query message,
            CancellationToken cancellationToken
        )
        {
            var article = await ArticleQueries.LoadArticleGraphAsync(
                db,
                x => x.Slug == message.Slug,
                cancellationToken
            );

            if (article == null)
            {
                throw new RestException(
                    HttpStatusCode.NotFound,
                    new { Article = Constants.NOT_FOUND }
                );
            }

            return new ArticleEnvelope(article);
        }
    }
}
