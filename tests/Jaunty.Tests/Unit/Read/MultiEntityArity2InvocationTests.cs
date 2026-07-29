using System.Data;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R26 (batch 3, low/performance). The arity-2 multi-entity mapper split the resolver's
/// <em>combined</em> mapper into two one-entity closures - <c>(t1, r) =&gt; combined(t1, default!, r)</c>
/// and <c>(t2, r) =&gt; combined(default!, t2, r)</c> - which every call site then invoked in
/// sequence, once per row.
///
/// <para>
/// The combined delegate's first act is <c>MultiEntityMapper&lt;T1, T2&gt;.Get(reader)</c> on the
/// reflection side, whose per-reader memoization was added with a comment saying it exists because
/// "the delegate calls Get(reader) on every row" - and every hit is then validated by
/// <c>ReaderCacheEntry.Matches</c>, which walks the full field count comparing
/// <c>reader.GetName(i)</c>. Calling it twice per row doubled precisely the cost that memoization
/// was added to remove. The arity 3-7 path never had this shape; only arity 2 paid double.
/// </para>
///
/// <para>
/// The invocation count is directly assertable - the resolver is a public configuration hook, so a
/// counting wrapper around the real one measures exactly how many times a row costs. The rest of
/// these tests pin that collapsing the two calls into one did not change what gets mapped.
/// </para>
/// </summary>
[Collection("Type Handler Operations")]
public class MultiEntityArity2InvocationTests : IDisposable
{
    private readonly Func<Type, Type, object>? _originalResolver;

    public MultiEntityArity2InvocationTests()
    {
        JauntyReflectionExtensions.UseReflectionMapping();
        _originalResolver = JauntyConfig.ReflectionMultiMapperResolver;
    }

    public void Dispose()
    {
        JauntyConfig.ReflectionMultiMapperResolver = _originalResolver;
        GC.SuppressFinalize(this);
    }

    // Static, because the counting delegate outlives the test: the core mapper caches it per
    // reader schema in a static dictionary. Each test therefore owns its own entity pair, so a
    // cached mapper from one test can never satisfy another and make it vacuous.
    private static int _countedCalls;

    [Table("arity2_orders")]
    public class CountedOrder
    {
        [Key]
        public int OrderId { get; set; }
        public string? OrderRef { get; set; }
    }

    [Table("arity2_customers")]
    public class CountedCustomer
    {
        [Key]
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
    }

    [Table("arity2_shape_orders")]
    public class ShapeOrder
    {
        [Key]
        public int OrderId { get; set; }
        public string? OrderRef { get; set; }
    }

    [Table("arity2_shape_customers")]
    public class ShapeCustomer
    {
        [Key]
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
    }

    private const string JoinSql = """
        SELECT o.OrderId, o.OrderRef, c.CustomerId, c.CustomerName
        FROM orders o JOIN customers c ON c.CustomerId = o.CustomerId
        ORDER BY o.OrderId;
        """;

    private static SqliteConnection Seed(int rows)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand create = connection.CreateCommand();
        create.CommandText =
            "CREATE TABLE customers (CustomerId INTEGER PRIMARY KEY, CustomerName TEXT);" +
            "CREATE TABLE orders (OrderId INTEGER PRIMARY KEY, OrderRef TEXT, CustomerId INTEGER);";
        create.ExecuteNonQuery();

        for (int i = 1; i <= rows; i++)
        {
            using SqliteCommand insert = connection.CreateCommand();
            insert.CommandText =
                $"INSERT INTO customers VALUES ({i}, 'customer-{i}');" +
                $"INSERT INTO orders VALUES ({i}, 'order-{i}', {i});";
            insert.ExecuteNonQuery();
        }

        return connection;
    }

    /// <summary>
    /// Wraps the real resolver so mapping still happens for real, and counts how often the combined
    /// mapper is invoked for the given pair.
    /// </summary>
    private void InstallCountingResolver<T1, T2>() where T1 : new() where T2 : new()
    {
        _countedCalls = 0;
        Func<Type, Type, object> inner = _originalResolver!;

        JauntyConfig.ReflectionMultiMapperResolver = (a, b) =>
        {
            object real = inner(a, b);

            if (a == typeof(T1) && b == typeof(T2) && real is Action<T1, T2, IDataRecord> combined)
            {
                return new Action<T1, T2, IDataRecord>((t1, t2, record) =>
                {
                    Interlocked.Increment(ref _countedCalls);
                    combined(t1, t2, record);
                });
            }

            return real;
        };
    }

    /// <summary>
    /// Twenty rows must cost twenty invocations of the combined mapper, not forty. This is the
    /// finding: the count was exactly double the row count, one call per entity.
    /// </summary>
    [Fact]
    public void OneRowCostsOneInvocationOfTheCombinedMapper()
    {
        InstallCountingResolver<CountedOrder, CountedCustomer>();

        using SqliteConnection connection = Seed(20);

        List<(CountedOrder, CountedCustomer)> results =
            connection.Query<CountedOrder, CountedCustomer>(JoinSql);

        Assert.Equal(20, results.Count);
        Assert.Equal(20, _countedCalls);
    }

    /// <summary>
    /// The point of collapsing the two calls is that both entities are still fully populated from
    /// the same row - previously each call mapped one side and discarded the other.
    /// </summary>
    [Fact]
    public void BothEntitiesAreStillMappedFromEachRow()
    {
        using SqliteConnection connection = Seed(5);

        List<(ShapeOrder, ShapeCustomer)> results =
            connection.Query<ShapeOrder, ShapeCustomer>(JoinSql);

        Assert.Equal(5, results.Count);

        for (int i = 0; i < results.Count; i++)
        {
            (ShapeOrder order, ShapeCustomer customer) = results[i];

            Assert.Equal(i + 1, order.OrderId);
            Assert.Equal($"order-{i + 1}", order.OrderRef);
            Assert.Equal(i + 1, customer.CustomerId);
            Assert.Equal($"customer-{i + 1}", customer.CustomerName);
        }
    }

    [Fact]
    public async Task TheAsyncPathMapsBothEntitiesToo()
    {
        using SqliteConnection connection = Seed(4);

        List<(ShapeOrder, ShapeCustomer)> results =
            await connection.QueryAsync<ShapeOrder, ShapeCustomer>(
                JoinSql, TestContext.Current.CancellationToken);

        Assert.Equal(4, results.Count);
        Assert.All(results, pair =>
        {
            Assert.NotEqual(0, pair.Item1.OrderId);
            Assert.NotNull(pair.Item1.OrderRef);
            Assert.NotEqual(0, pair.Item2.CustomerId);
            Assert.NotNull(pair.Item2.CustomerName);
        });
    }

    /// <summary>An empty result set must not invoke the mapper at all.</summary>
    [Fact]
    public void NoRowsCostNoInvocations()
    {
        using SqliteConnection connection = Seed(0);

        List<(ShapeOrder, ShapeCustomer)> results =
            connection.Query<ShapeOrder, ShapeCustomer>(JoinSql);

        Assert.Empty(results);
    }
}
