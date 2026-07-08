using System;
using System.Threading;
using System.Threading.Tasks;

using MediatR;

namespace Conduit.Infrastructure;

/// <summary>
/// Adds a transaction to the processing pipeline. Jaunty-based replacement for the former
/// EF Core <c>DBContextTransactionPipelineBehavior</c>.
/// </summary>
public class ConduitDbTransactionPipelineBehavior<TRequest, TResponse>(ConduitDb db)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        TResponse? result;

        try
        {
            db.BeginTransaction();

            result = await next();

            db.CommitTransaction();
        }
        catch (Exception)
        {
            db.RollbackTransaction();
            throw;
        }

        return result;
    }
}
