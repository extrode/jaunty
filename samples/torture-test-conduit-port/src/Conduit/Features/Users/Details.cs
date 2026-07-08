using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using Conduit.Infrastructure.Security;
using FluentValidation;
using MediatR;
using Jaunty;
using Jaunty.Fluent;

namespace Conduit.Features.Users;

public class Details
{
    public record Query(string Username) : IRequest<UserEnvelope>;

    public class QueryValidator : AbstractValidator<Query>
    {
        public QueryValidator() => RuleFor(x => x.Username).NotNull().NotEmpty();
    }

    public class QueryHandler(
        ConduitDb db,
        IJwtTokenGenerator jwtTokenGenerator,
        IMapper mapper
    ) : IRequestHandler<Query, UserEnvelope>
    {
        public async Task<UserEnvelope> Handle(Query message, CancellationToken cancellationToken)
        {
            var person = await db.Connection
                .From<Domain.Person>()
                .Where(x => x.Username == message.Username)
                .SelectFirstOrDefaultAsync(cancellationToken);

            if (person == null)
            {
                throw new RestException(
                    HttpStatusCode.NotFound,
                    new { User = Constants.NOT_FOUND }
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
