using System;
using System.Collections.Generic;
using System.Linq;
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

public class Edit
{
    public record ArticleData(string? Title, string? Description, string? Body, string[]? TagList);

    public record Command(Model Model, string Slug) : IRequest<ArticleEnvelope>;

    public record Model(ArticleData Article);

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() => RuleFor(x => x.Model.Article).NotNull();
    }

    public class Handler(ConduitDb db) : IRequestHandler<Command, ArticleEnvelope>
    {
        public async Task<ArticleEnvelope> Handle(
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

            article.ArticleTags = await db.Connection
                .From<ArticleTag>()
                .WhereIn(t => t.ArticleId, [article.ArticleId])
                .SelectAsync(cancellationToken);

            var wasModified = false;

            if (message.Model.Article.Description is not null && message.Model.Article.Description != article.Description)
            {
                article.Description = message.Model.Article.Description;
                wasModified = true;
            }

            if (message.Model.Article.Body is not null && message.Model.Article.Body != article.Body)
            {
                article.Body = message.Model.Article.Body;
                wasModified = true;
            }

            if (message.Model.Article.Title is not null && message.Model.Article.Title != article.Title)
            {
                article.Title = message.Model.Article.Title;
                article.Slug = article.Title.GenerateSlug();
                wasModified = true;
            }

            // list of currently saved article tags for the given article
            var articleTagList = message.Model.Article.TagList ?? Enumerable.Empty<string>();

            var articleTagsToCreate = GetArticleTagsToCreate(article, articleTagList);
            var articleTagsToDelete = GetArticleTagsToDelete(article, articleTagList);

            if (wasModified || articleTagsToCreate.Count != 0 || articleTagsToDelete.Count != 0)
            {
                article.UpdatedAt = DateTime.UtcNow;
            }

            await db.Connection.UpdateAsync(article, db.Options, cancellationToken);

            // ensure any tags about to be created exist so the join insert doesn't hit a missing FK
            foreach (var articleTag in articleTagsToCreate)
            {
                var existingTag = await db.Connection
                    .From<Tag>()
                    .Where(x => x.TagId == articleTag.TagId)
                    .SelectFirstOrDefaultAsync(cancellationToken);

                if (existingTag is null)
                {
                    await db.Connection.InsertAsync(
                        new Tag { TagId = articleTag.TagId },
                        db.Options,
                        cancellationToken
                    );
                }

                await db.Connection.InsertAsync(articleTag, db.Options, cancellationToken);
            }

            foreach (var articleTag in articleTagsToDelete)
            {
                await db.Connection.DeleteAsync(articleTag, db.Options, cancellationToken);
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

        /// <summary>
        /// check which article tags need to be added
        /// </summary>
        private static List<ArticleTag> GetArticleTagsToCreate(
            Article article,
            IEnumerable<string> articleTagList
        )
        {
            var articleTagsToCreate = new List<ArticleTag>();
            foreach (var tag in articleTagList)
            {
                var at = article.ArticleTags?.FirstOrDefault(t => t.TagId == tag);
                if (at == null)
                {
                    at = new ArticleTag { ArticleId = article.ArticleId, TagId = tag };
                    articleTagsToCreate.Add(at);
                }
            }

            return articleTagsToCreate;
        }

        /// <summary>
        /// check which article tags need to be deleted
        /// </summary>
        private static List<ArticleTag> GetArticleTagsToDelete(
            Article article,
            IEnumerable<string> articleTagList
        )
        {
            var articleTagsToDelete = new List<ArticleTag>();
            foreach (var tag in article.ArticleTags)
            {
                var at = articleTagList.FirstOrDefault(t => t == tag.TagId);
                if (at == null)
                {
                    articleTagsToDelete.Add(tag);
                }
            }

            return articleTagsToDelete;
        }
    }
}
