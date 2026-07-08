using System.Net;
using System.Threading.Tasks;

using Conduit.Domain;
using Conduit.Features.Comments;
using Conduit.Infrastructure.Errors;
using Conduit.IntegrationTests.Features.Users;

using Jaunty;
using Jaunty.Fluent;

namespace Conduit.IntegrationTests.Features.Comments;

public static class CommentHelpers
{
    /// <summary>
    /// creates an article comment based on the given Create command.
    /// Creates a default user if parameter userName is empty.
    /// </summary>
    /// <param name="fixture"></param>
    /// <param name="command"></param>
    /// <param name="userName"></param>
    /// <returns></returns>
    public static async Task<Domain.Comment> CreateComment(
        SliceFixture fixture,
        Create.Command command,
        string userName
    )
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            var user = await UserHelpers.CreateDefaultUser(fixture);

            if (user.Username is null)
            {
                throw new RestException(HttpStatusCode.BadRequest);
            }

            userName = user.Username;
        }

        var conduitDb = fixture.GetConduitDb();
        var currentAccessor = new StubCurrentUserAccessor(userName);

        var commentCreateHandler = new Create.Handler(conduitDb, currentAccessor);
        var created = await commentCreateHandler.Handle(
            command,
            new System.Threading.CancellationToken()
        );

        var dbComment = await fixture.ExecuteConduitDbAsync(db =>
            db.Connection
                .From<Comment>()
                .Where(c => c.CommentId == created.Comment.CommentId)
                .SelectSingleOrDefaultAsync()
        );

        if (dbComment is null)
        {
            throw new RestException(HttpStatusCode.NotFound, new { Article = Constants.NOT_FOUND });
        }

        return dbComment;
    }
}
