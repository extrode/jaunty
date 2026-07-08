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

namespace Conduit.Features.Comments;

public class Delete
{
    public record Command(string Slug, int Id) : IRequest;

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

            var comment = await db.Connection
                .From<Comment>()
                .Where(x => x.ArticleId == article.ArticleId && x.CommentId == message.Id)
                .SelectFirstOrDefaultAsync(cancellationToken)
                ?? throw new RestException(
                    HttpStatusCode.NotFound,
                    new { Comment = Constants.NOT_FOUND }
                );

            await db.Connection.DeleteAsync(comment, db.Options, cancellationToken);
        }
    }
}
