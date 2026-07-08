using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using AutoMapper;

using Conduit.Domain;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;

using Jaunty;
using Jaunty.Fluent;

namespace Conduit.Features.Profiles;

public class ProfileReader(
    ConduitDb db,
    ICurrentUserAccessor currentUserAccessor,
    IMapper mapper
) : IProfileReader
{
    public async Task<ProfileEnvelope> ReadProfile(
        string username,
        CancellationToken cancellationToken
    )
    {
        var currentUserName = currentUserAccessor.GetCurrentUsername();

        var person = await db.Connection
            .From<Person>()
            .Where(x => x.Username == username)
            .SelectFirstOrDefaultAsync(cancellationToken);

        if (person is null)
        {
            throw new RestException(HttpStatusCode.NotFound, new { User = Constants.NOT_FOUND });
        }

        var profile = mapper.Map<Domain.Person, Profile>(person);

        if (currentUserName != null)
        {
            var currentPerson = await db.Connection
                .From<Person>()
                .Where(x => x.Username == currentUserName)
                .SelectFirstOrDefaultAsync(cancellationToken);

            if (currentPerson is null)
            {
                throw new RestException(
                    HttpStatusCode.NotFound,
                    new { User = Constants.NOT_FOUND }
                );
            }

            var followers = await db.Connection
                .From<FollowedPeople>()
                .WhereIn(f => f.ObserverId, [currentPerson.PersonId])
                .SelectAsync(cancellationToken);

            if (followers.Any(x => x.TargetId == person.PersonId))
            {
                profile.IsFollowed = true;
            }
        }

        return new ProfileEnvelope(profile);
    }
}
