using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using AutoMapper;

using Conduit.Domain;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using Conduit.Infrastructure.Security;

using FluentValidation;

using Jaunty;
using Jaunty.Fluent;

using MediatR;

namespace Conduit.Features.Users;

public class Edit
{
    public class UserData
    {
        public string? Username { get; set; }

        public string? Email { get; set; }

        public string? Password { get; set; }

        public string? Bio { get; set; }

        public string? Image { get; set; }
    }

    public record Command(UserData User) : IRequest<UserEnvelope>;

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() => RuleFor(x => x.User).NotNull();
    }

    public class Handler(
        ConduitDb db,
        IPasswordHasher passwordHasher,
        ICurrentUserAccessor currentUserAccessor,
        IMapper mapper
    ) : IRequestHandler<Command, UserEnvelope>
    {
        public async Task<UserEnvelope> Handle(Command message, CancellationToken cancellationToken)
        {
            var currentUsername = currentUserAccessor.GetCurrentUsername();
            var person = await db.Connection
                .From<Person>()
                .Where(x => x.Username == currentUsername)
                .SelectFirstOrDefaultAsync(cancellationToken);

            if (person is null)
            {
                throw new RestException(
                    HttpStatusCode.NotFound,
                    new { User = Constants.NOT_FOUND }
                );
            }

            person.Username = message.User.Username ?? person.Username;
            person.Email = message.User.Email ?? person.Email;
            person.Bio = message.User.Bio ?? person.Bio;
            person.Image = message.User.Image ?? person.Image;

            if (!string.IsNullOrWhiteSpace(message.User.Password))
            {
                var salt = Guid.NewGuid().ToByteArray();
                person.Hash = await passwordHasher.Hash(message.User.Password, salt);
                person.Salt = salt;
            }

            await db.Connection.UpdateAsync(person, db.Options, cancellationToken);

            return new UserEnvelope(mapper.Map<Domain.Person, User>(person));
        }
    }
}
