using System;
using System.Threading.Tasks;

using Conduit.Domain;
using Conduit.Features.Articles;
using Conduit.IntegrationTests.Features.Comments;
using Conduit.IntegrationTests.Features.Users;

using Jaunty;
using Jaunty.Fluent;

using Xunit;

namespace Conduit.IntegrationTests.Features.Articles;

public class DeleteTests : SliceFixture
{
    [Fact]
    public async Task Expect_Delete_Article()
    {
        var createCmd = new Create.Command(
            new Create.ArticleData
            {
                Title = "Test article dsergiu77",
                Description = "Description of the test article",
                Body = "Body of the test article",
            }
        );

        var article = await ArticleHelpers.CreateArticle(this, createCmd);
        var slug = article.Slug ?? throw new InvalidOperationException();

        var deleteCmd = new Delete.Command(slug);

        var conduitDb = GetConduitDb();

        var articleDeleteHandler = new Delete.QueryHandler(conduitDb);
        await articleDeleteHandler.Handle(deleteCmd, new System.Threading.CancellationToken());

        var dbArticle = await ExecuteConduitDbAsync(db =>
            db.Connection
                .From<Article>()
                .Where(d => d.Slug == deleteCmd.Slug)
                .SelectSingleOrDefaultAsync()
        );

        Assert.Null(dbArticle);
    }

    [Fact]
    public async Task Expect_Delete_Article_With_Tags()
    {
        var createCmd = new Create.Command(
            new Create.ArticleData
            {
                Title = "Test article dsergiu77",
                Description = "Description of the test article",
                Body = "Body of the test article",
                TagList = ["tag1", "tag2"],
            }
        );

        var article = await ArticleHelpers.CreateArticle(this, createCmd);
        var dbArticleWithTags = await ExecuteConduitDbAsync(db =>
            db.Connection
                .From<Article>()
                .Where(d => d.Slug == article.Slug)
                .SelectSingleOrDefaultAsync()
        );

        var deleteCmd = new Delete.Command(article.Slug ?? throw new InvalidOperationException());

        var conduitDb = GetConduitDb();

        var articleDeleteHandler = new Delete.QueryHandler(conduitDb);
        await articleDeleteHandler.Handle(deleteCmd, new System.Threading.CancellationToken());

        var dbArticle = await ExecuteConduitDbAsync(db =>
            db.Connection
                .From<Article>()
                .Where(d => d.Slug == deleteCmd.Slug)
                .SelectSingleOrDefaultAsync()
        );
        Assert.Null(dbArticle);
    }

    [Fact]
    public async Task Expect_Delete_Article_With_Comments()
    {
        var createArticleCmd = new Create.Command(
            new Create.ArticleData
            {
                Title = "Test article dsergiu77",
                Description = "Description of the test article",
                Body = "Body of the test article",
            }
        );

        var article = await ArticleHelpers.CreateArticle(this, createArticleCmd);
        var dbArticle =
            await ExecuteConduitDbAsync(db =>
                db.Connection
                    .From<Article>()
                    .Where(d => d.Slug == article.Slug)
                    .SelectSingleOrDefaultAsync()
            ) ?? throw new InvalidOperationException();

        var articleId = dbArticle.ArticleId;
        var slug = dbArticle.Slug;

        // create article comment
        var createCommentCmd = new Conduit.Features.Comments.Create.Command(
            new(new Conduit.Features.Comments.Create.CommentData("article comment")),
            slug ?? throw new InvalidOperationException()
        );

        var comment = await CommentHelpers.CreateComment(
            this,
            createCommentCmd,
            UserHelpers.DefaultUserName
        );

        // delete article with comment
        var deleteCmd = new Delete.Command(slug);

        var conduitDb = GetConduitDb();

        var articleDeleteHandler = new Delete.QueryHandler(conduitDb);
        await articleDeleteHandler.Handle(deleteCmd, new System.Threading.CancellationToken());

        var deleted = await ExecuteConduitDbAsync(db =>
            db.Connection
                .From<Article>()
                .Where(d => d.Slug == deleteCmd.Slug)
                .SelectSingleOrDefaultAsync()
        );
        Assert.Null(deleted);
    }
}
