using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Conduit.Domain;
using Conduit.Infrastructure;

using Jaunty.Fluent;

using MediatR;

namespace Conduit.Features.Tags;

public class List
{
    public record Query : IRequest<TagsEnvelope>;

    public class QueryHandler(ConduitDb db) : IRequestHandler<Query, TagsEnvelope>
    {
        public async Task<TagsEnvelope> Handle(Query message, CancellationToken cancellationToken)
        {
            var tags = await db.Connection
                .From<Tag>()
                .OrderBy(x => x.TagId)
                .SelectAsync(cancellationToken);

            return new TagsEnvelope
            {
                Tags = tags?.Select(x => x.TagId ?? string.Empty).ToList() ?? new List<string>(),
            };
        }
    }
}
