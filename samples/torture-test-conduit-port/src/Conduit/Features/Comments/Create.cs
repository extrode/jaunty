using System;
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

public class Create
{
    public record CommentData(string? Body);

    public record Command(Model Model, string Slug) : IRequest<CommentEnvelope>;

    public record Model(CommentData Comment) : IRequest<CommentEnvelope>;

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() => RuleFor(x => x.Model.Comment.Body).NotEmpty();
    }

    public class Handler(ConduitDb db, ICurrentUserAccessor currentUserAccessor)
        : IRequestHandler<Command, CommentEnvelope>
    {
        public async Task<CommentEnvelope> Handle(
            Command message,
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

            var author = await db.Connection
                .From<Person>()
                .Where(x => x.Username == currentUserAccessor.GetCurrentUsername())
                .SelectFirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Current user not found.");

            var comment = new Comment
            {
                AuthorId = author.PersonId,
                Author = author,
                ArticleId = article.ArticleId,
                Article = article,
                Body = message.Model.Comment.Body ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            var newCommentId = await db.Connection.InsertAsync(comment, db.Options, cancellationToken);
            comment.CommentId = (int)newCommentId;

            return new CommentEnvelope(comment);
        }
    }
}
