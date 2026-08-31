using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Conduit.Domain;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using Conduit.Infrastructure.Security;
using FluentValidation;
using MediatR;
using Jaunty;
using Jaunty.Fluent;

namespace Conduit.Features.Users;

public class Create
{
    public record UserData(string? Username, string? Email, string? Password);

    public record Command(UserData User) : IRequest<UserEnvelope>;

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.User.Username).NotNull().NotEmpty();
            RuleFor(x => x.User.Email).NotNull().NotEmpty();
            RuleFor(x => x.User.Password).NotNull().NotEmpty();
        }
    }

    public class Handler(
        ConduitDb db,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IMapper mapper
    ) : IRequestHandler<Command, UserEnvelope>
    {
        public async Task<UserEnvelope> Handle(Command message, CancellationToken cancellationToken)
        {
            var usernameCount = db.Connection
                .From<Person>()
                .Where(x => x.Username == message.User.Username)
                .Count();

            if (usernameCount > 0)
            {
                throw new RestException(
                    HttpStatusCode.BadRequest,
                    new { Username = Constants.IN_USE }
                );
            }

            var emailCount = db.Connection
                .From<Person>()
                .Where(x => x.Email == message.User.Email)
                .Count();

            if (emailCount > 0)
            {
                throw new RestException(
                    HttpStatusCode.BadRequest,
                    new { Email = Constants.IN_USE }
                );
            }

            var salt = Guid.NewGuid().ToByteArray();
            var person = new Person
            {
                Username = message.User.Username,
                Email = message.User.Email,
                Hash = await passwordHasher.Hash(
                    message.User.Password ?? throw new InvalidOperationException(),
                    salt
                ),
                Salt = salt,
            };

            var newPersonId = await db.Connection.InsertAsync(person, db.Options, cancellationToken);
            person.PersonId = (int)newPersonId;

            var user = mapper.Map<Person, User>(person);
            user.Token = jwtTokenGenerator.CreateToken(
                person.Username ?? throw new InvalidOperationException()
            );
            return new UserEnvelope(user);
        }
    }
}
