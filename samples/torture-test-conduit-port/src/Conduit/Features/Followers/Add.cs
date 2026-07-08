using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Conduit.Domain;
using Conduit.Features.Profiles;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;

using FluentValidation;

using Jaunty;
using Jaunty.Fluent;

using MediatR;

namespace Conduit.Features.Followers;

public class Add
{
    public record Command(string Username) : IRequest<ProfileEnvelope>;

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() => RuleFor(x => x.Username).NotNull().NotEmpty();
    }

    public class QueryHandler(
        ConduitDb db,
        ICurrentUserAccessor currentUserAccessor,
        IProfileReader profileReader
    ) : IRequestHandler<Command, ProfileEnvelope>
    {
        public async Task<ProfileEnvelope> Handle(
            Command message,
            CancellationToken cancellationToken
        )
        {
            var target = await db.Connection
                .From<Person>()
                .Where(x => x.Username == message.Username)
                .SelectFirstOrDefaultAsync(cancellationToken);

            if (target is null)
            {
                throw new RestException(
                    HttpStatusCode.NotFound,
                    new { User = Constants.NOT_FOUND }
                );
            }

            var observer = await db.Connection
                .From<Person>()
                .Where(x => x.Username == currentUserAccessor.GetCurrentUsername())
                .SelectFirstOrDefaultAsync(cancellationToken);

            if (observer is null)
            {
                throw new RestException(
                    HttpStatusCode.NotFound,
                    new { User = Constants.NOT_FOUND }
                );
            }

            var followedPeople = await db.Connection
                .From<FollowedPeople>()
                .Where(x => x.ObserverId == observer.PersonId && x.TargetId == target.PersonId)
                .SelectFirstOrDefaultAsync(cancellationToken);

            if (followedPeople == null)
            {
                followedPeople = new FollowedPeople
                {
                    ObserverId = observer.PersonId,
                    Observer = observer,
                    TargetId = target.PersonId,
                    Target = target,
                };
                await db.Connection.InsertAsync(followedPeople, db.Options, cancellationToken);
            }

            return await profileReader.ReadProfile(message.Username, cancellationToken);
        }
    }
}
