using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Conduit.Domain;
using Conduit.Infrastructure;

using FluentValidation;

using Jaunty;
using Jaunty.Fluent;

using MediatR;

namespace Conduit.Features.Articles;

public class Create
{
    public class ArticleData
    {
        public string? Title { get; init; }

        public string? Description { get; init; }

        public string? Body { get; init; }

        public string[]? TagList { get; init; }
    }

    public class ArticleDataValidator : AbstractValidator<ArticleData>
    {
        public ArticleDataValidator()
        {
            RuleFor(x => x.Title).NotNull().NotEmpty();
            RuleFor(x => x.Description).NotNull().NotEmpty();
            RuleFor(x => x.Body).NotNull().NotEmpty();
        }
    }

    public record Command(ArticleData Article) : IRequest<ArticleEnvelope>;

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() =>
            RuleFor(x => x.Article).NotNull().SetValidator(new ArticleDataValidator());
    }

    public class Handler(ConduitDb db, ICurrentUserAccessor currentUserAccessor)
        : IRequestHandler<Command, ArticleEnvelope>
    {
        public async Task<ArticleEnvelope> Handle(
            Command message,
            CancellationToken cancellationToken
        )
        {
            var author = await db.Connection
                .From<Person>()
                .Where(x => x.Username == currentUserAccessor.GetCurrentUsername())
                .SelectFirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Current user not found.");

            var tags = new List<Tag>();
            foreach (var tag in message.Article.TagList ?? Enumerable.Empty<string>())
            {
                var t = await db.Connection
                    .From<Tag>()
                    .Where(x => x.TagId == tag)
                    .SelectFirstOrDefaultAsync(cancellationToken);

                if (t == null)
                {
                    t = new Tag { TagId = tag };
                    await db.Connection.InsertAsync(t, db.Options, cancellationToken);
                }

                tags.Add(t);
            }

            var article = new Article
            {
                AuthorId = author.PersonId,
                Author = author,
                Body = message.Article.Body,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Description = message.Article.Description,
                Title = message.Article.Title,
                Slug = message.Article.Title.GenerateSlug(),
            };

            var newArticleId = await db.Connection.InsertAsync(article, db.Options, cancellationToken);
            article.ArticleId = (int)newArticleId;

            var articleTags = tags
                .Select(t => new ArticleTag { ArticleId = article.ArticleId, TagId = t.TagId, Article = article, Tag = t })
                .ToList();

            foreach (var articleTag in articleTags)
            {
                await db.Connection.InsertAsync(articleTag, db.Options, cancellationToken);
            }

            article.ArticleTags = articleTags;

            return new ArticleEnvelope(article);
        }
    }
}
