using System.Net;
using System.Threading.Tasks;

using Conduit.Domain;
using Conduit.Features.Articles;
using Conduit.Infrastructure.Errors;
using Conduit.IntegrationTests.Features.Users;

namespace Conduit.IntegrationTests.Features.Articles;

public static class ArticleHelpers
{
    /// <summary>
    /// creates an article based on the given Create command. It also creates a default user
    /// </summary>
    /// <param name="fixture"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public static async Task<Domain.Article> CreateArticle(
        SliceFixture fixture,
        Create.Command command
    )
    {
        // first create the default user
        var user = await UserHelpers.CreateDefaultUser(fixture);
        if (user.Username is null)
        {
            throw new RestException(HttpStatusCode.BadRequest);
        }

        var conduitDb = fixture.GetConduitDb();
        var currentAccessor = new StubCurrentUserAccessor(user.Username);

        var articleCreateHandler = new Create.Handler(conduitDb, currentAccessor);
        var created = await articleCreateHandler.Handle(
            command,
            new System.Threading.CancellationToken()
        );

        var dbArticle = await fixture.ExecuteConduitDbAsync(db =>
            ArticleQueries.LoadArticleGraphAsync(
                db,
                a => a.ArticleId == created.Article.ArticleId,
                System.Threading.CancellationToken.None
            )
        );
        if (dbArticle is null)
        {
            throw new RestException(HttpStatusCode.NotFound, new { Article = Constants.NOT_FOUND });
        }

        return dbArticle;
    }
}
