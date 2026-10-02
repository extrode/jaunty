using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Extrode.Jaunty.Extensions.Reflection.BulkCopy;

/// <summary>
/// Runs a provider body that calls the driver through <c>MethodInfo.Invoke</c>,
/// rethrowing what the driver threw rather than the <see cref="TargetInvocationException"/> around it.
/// </summary>
/// <remarks>
/// AUD-R38-014/015: the synchronous copy paths surfaced a server error - a duplicate key, a CHECK
/// violation - as <see cref="TargetInvocationException"/>, while the asynchronous paths awaited
/// the driver's task and surfaced the provider's own exception, so a caller's
/// <c>catch (PostgresException)</c> or <c>catch (SqlException)</c> worked on one path only.
/// </remarks>
internal static class ReflectedCall
{
    public static T Unwrapped<T>(Func<T> body)
    {
        try
        {
            return body();
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    public static async ValueTask<T> UnwrappedAsync<T>(Func<ValueTask<T>> body)
    {
        try
        {
            return await body().ConfigureAwait(false);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}
