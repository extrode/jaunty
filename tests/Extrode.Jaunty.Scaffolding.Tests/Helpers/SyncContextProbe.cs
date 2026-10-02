namespace Extrode.Jaunty.Scaffolding.Tests.Helpers;

internal sealed class CountingContext : SynchronizationContext
{
    private int _posts;

    public int Posts => Volatile.Read(ref _posts);

    public override void Post(SendOrPostCallback d, object? state)
    {
        Interlocked.Increment(ref _posts);
        ThreadPool.QueueUserWorkItem(_ => d(state));
    }

    public static T Run<T>(CountingContext context, Func<Task<T>> body)
        => Task.Run(() =>
        {
            SynchronizationContext? previous = Current;
            SetSynchronizationContext(context);
            try
            {
                return body().GetAwaiter().GetResult();
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }).GetAwaiter().GetResult();
}
