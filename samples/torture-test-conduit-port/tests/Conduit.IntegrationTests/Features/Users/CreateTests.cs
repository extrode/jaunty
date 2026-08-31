using System.Threading.Tasks;

using Conduit.Domain;
using Conduit.Features.Users;
using Conduit.Infrastructure.Security;

using Jaunty;
using Jaunty.Fluent;

using Xunit;

namespace Conduit.IntegrationTests.Features.Users;

public class CreateTests : SliceFixture
{
    [Fact]
    public async Task Expect_Create_User()
    {
        var command = new Create.Command(new Create.UserData("username", "email", "password"));

        await SendAsync(command);

        var created = await ExecuteConduitDbAsync(db =>
            db.Connection
                .From<Person>()
                .Where(d => d.Email == command.User.Email)
                .SelectSingleOrDefaultAsync()
        );

        Assert.NotNull(created);
        Assert.Equal(created.Hash, await new PasswordHasher().Hash("password", created.Salt));
    }
}
