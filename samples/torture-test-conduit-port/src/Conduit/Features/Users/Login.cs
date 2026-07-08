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

public class Login
{
    public class UserData
    {
        public string? Email { get; init; }

        public string? Password { get; init; }
    }

    public record Command(UserData User) : IRequest<UserEnvelope>;

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.User).NotNull();
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
            var person = await db.Connection
                .From<Person>()
                .Where(x => x.Email == message.User.Email)
                .SelectSingleOrDefaultAsync(cancellationToken);
            if (person == null)
            {
                throw new RestException(
                    HttpStatusCode.Unauthorized,
                    new { Error = "Invalid email / password." }
                );
            }

            var hash = await passwordHasher.Hash(
                message.User.Password ?? throw new InvalidOperationException(),
                person.Salt
            );

            if (!person.Hash.SequenceEqual(hash))
            {
                throw new RestException(
                    HttpStatusCode.Unauthorized,
                    new { Error = "Invalid email / password." }
                );
            }

            var user = mapper.Map<Domain.Person, User>(person);
            user.Token = jwtTokenGenerator.CreateToken(
                person.Username ?? throw new InvalidOperationException()
            );
            return new UserEnvelope(user);
        }
    }
}
