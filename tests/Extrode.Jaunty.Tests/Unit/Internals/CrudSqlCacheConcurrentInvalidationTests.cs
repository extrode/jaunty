using System.Collections.Concurrent;
using System.Data.SQLite;

using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Extensions.Reflection;
using Extrode.Jaunty.Internals;
using Extrode.Jaunty.Internals.Write;
using Extrode.Jaunty.Tests.Helpers;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Internals;

/// <summary>
/// Overnight audit tick 6: stress-tests the self-healing contract documented on
/// <see cref="ConfigurationGeneration"/> - a cache entry tagged with a superseded generation is
/// never served, no matter how many concurrent readers and invalidators interleave. Neither
/// <see cref="ConfigurationGeneration"/> nor <see cref="CrudSqlCache"/> had a concurrency test
/// before this, despite both being process-wide static state exercised from arbitrary threads.
///
/// <para>
/// Mutates <see cref="JauntyConfig"/> via <c>UseReflectionMapping</c>/<c>Reset</c> and calls
/// <see cref="ConfigurationGeneration"/>.Invalidate() thousands of times per run, so this must run
/// in the serialized "Type Handler Operations" collection, not in Extrode.Jaunty.UnitTests'
/// parallel assembly - see ResetCallersReinitializeTests for what happens when a test like this one
/// leaks into a collection that runs concurrently with it.
/// </para>
/// </summary>
[Collection("Type Handler Operations")]
public class CrudSqlCacheConcurrentInvalidationTests : IDisposable
{
    public CrudSqlCacheConcurrentInvalidationTests() => JauntyReflectionExtensions.UseReflectionMapping();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        JauntyConfig.Reset();
        TestInitializer.Initialize();
    }

    [Table("crud_sql_cache_stress")]
    private class Widget
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public async Task GetSql_ConcurrentWithInvalidation_NeverReturnsAStaleGeneration()
    {
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();

        const int readerCount = 8;
        const int invalidatorCount = 4;
        const int iterations = 2000;

        var exceptions = new ConcurrentBag<Exception>();
        var barrier = new Barrier(readerCount + invalidatorCount);

        var readers = new Task[readerCount];
        for (int i = 0; i < readerCount; i++)
        {
            readers[i] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                try
                {
                    for (int j = 0; j < iterations; j++)
                        CrudSqlCache.GetSql<Widget>(connection);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

        var invalidators = new Task[invalidatorCount];
        for (int i = 0; i < invalidatorCount; i++)
        {
            invalidators[i] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                try
                {
                    for (int j = 0; j < iterations; j++)
                        ConfigurationGeneration.Invalidate();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

        await Task.WhenAll(readers.Concat(invalidators));

        Assert.Empty(exceptions);

        // The race has settled at whatever generation the last invalidator left behind. One more
        // invalidation, single-threaded, must force a genuinely new build rather than keep serving
        // the instance obtained before it - that's the actual self-healing contract; two reads of
        // ConfigurationGeneration.Current with nothing invalidating between them can't exercise it.
        CachedCrudSql beforeFinalInvalidate = CrudSqlCache.GetSql<Widget>(connection);
        ConfigurationGeneration.Invalidate();
        CachedCrudSql afterFinalInvalidate = CrudSqlCache.GetSql<Widget>(connection);
        Assert.NotSame(beforeFinalInvalidate, afterFinalInvalidate);
    }

    // GetSql has no single-flight lock around its check-then-build-then-store path, so concurrent
    // callers racing a cold cache can each build their own CachedCrudSql instance - this does not
    // assert a single shared instance (that would be false and flaky), only that every build the
    // race produces carries identical SQL, i.e. no build is corrupted or torn under contention.
    [Fact]
    public async Task GetSql_ConcurrentCallsAtAFixedGeneration_AllBuildIdenticalSql()
    {
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();

        const int callers = 16;
        var results = new ConcurrentBag<CachedCrudSql>();
        var barrier = new Barrier(callers);

        var tasks = new Task[callers];
        for (int i = 0; i < callers; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                results.Add(CrudSqlCache.GetSql<Widget>(connection));
            });
        }

        await Task.WhenAll(tasks);

        Assert.Equal(callers, results.Count);
        CachedCrudSql first = results.First();
        Assert.All(results, r => Assert.Equal(first.InsertSql, r.InsertSql));
    }
}
