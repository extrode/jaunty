using System.Collections.Concurrent;
using System.Data;
using System.Reflection;

using Jaunty.Attributes;
using Jaunty.Internals.Parameters;

using Xunit;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// AUD-R25 batch 3: four defects in the per-command parameter binding path.
///
/// <list type="bullet">
/// <item><description>
/// B3-1 - <c>CommandTemplate._templates</c> was lazily initialized with a bare <c>??=</c>. The
/// instance lives in the static <c>TemplateCache</c> and is handed to any thread binding the same
/// (sql, paramType, commandType), so the array reference was published with no release barrier;
/// under a weak memory model a reader could see a non-null array whose element writes were not yet
/// visible and dereference a null element.
/// </description></item>
/// <item><description>
/// B3-2 - <c>BoundedCache.Evict</c> looped on <c>ConcurrentDictionary.Count</c>, which acquires
/// every bucket lock, after every successful insert.
/// </description></item>
/// <item><description>
/// B3-3 - the un-expanded collection-property path re-parsed SQL that <c>Bind</c> had already
/// parsed and discarded.
/// </description></item>
/// <item><description>
/// B3-5 - an unreachable "nullable enums" branch in <c>ApplyTypeHandlerIfNeeded</c>.
/// </description></item>
/// </list>
/// </summary>
public class ParameterBinderInternalsTests
{
    // ------------------------------------------------------------------
    // B3-1: concurrent first-bind of a shared template
    // ------------------------------------------------------------------

    [Fact]
    public void Bind_SharedTemplateBoundConcurrently_NeverSeesAPartiallyPublishedTemplateArray()
    {
        // Hammers the exact race: many threads reaching a freshly cached CommandTemplate's first
        // Bind at once. Under the old bare "??=" the array reference could be observed before its
        // element writes, giving an intermittent NullReferenceException inside CloneParameter.
        const int Threads = 32;
        const int BindsPerThread = 50;

        var sql = $"SELECT * FROM T WHERE A = @A AND B = @B AND C = @C -- {Guid.NewGuid():N}";
        var exceptions = new ConcurrentBag<Exception>();

        using var barrier = new Barrier(Threads);
        var workers = new Thread[Threads];

        for (int t = 0; t < Threads; t++)
        {
            workers[t] = new Thread(() =>
            {
                try
                {
                    barrier.SignalAndWait();
                    for (int i = 0; i < BindsPerThread; i++)
                    {
                        var command = new FakeCommand { CommandText = sql };
                        ParameterBinder.Bind(command, new { A = 1, B = "two", C = 3.0 });
                        Assert.Equal(3, command.Parameters.Count);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

        foreach (Thread worker in workers) worker.Start();
        foreach (Thread worker in workers) worker.Join();

        Assert.Empty(exceptions);
    }

    [Fact]
    public void Bind_RepeatedBindsOfTheSameSql_ProduceIdenticalParameterSets()
    {
        // The cached template must keep binding correctly after the first call publishes it.
        var sql = $"SELECT * FROM T WHERE A = @A AND B = @B -- {Guid.NewGuid():N}";

        for (int i = 0; i < 5; i++)
        {
            var command = new FakeCommand { CommandText = sql };
            ParameterBinder.Bind(command, new { A = i, B = "x" });

            Assert.Equal(2, command.Parameters.Count);
            Assert.Equal(i, ((IDbDataParameter)command.Parameters[0]!).Value);
            Assert.Equal("x", ((IDbDataParameter)command.Parameters[1]!).Value);
        }
    }

    // ------------------------------------------------------------------
    // B3-3: the un-expanded collection-property path
    // ------------------------------------------------------------------

    [Fact]
    public void Bind_CollectionPropertyThatIsNull_StillBindsTheScalarParameters()
    {
        // This shape routes through BindDynamic with the original SQL rather than a cached template,
        // which is where the redundant re-parse lived. Behaviour must be unchanged.
        var command = new FakeCommand { CommandText = "SELECT * FROM T WHERE A = @A AND Id IN @Ids" };

        ParameterBinder.Bind(command, new NullableCollectionParams { A = 7, Ids = null });

        Assert.Equal(2, command.Parameters.Count);
        Assert.Equal(7, ((IDbDataParameter)command.Parameters[0]!).Value);
    }

    [Fact]
    public void Bind_CollectionPropertyWithValues_StillExpandsTheInClause()
    {
        // The other half of the branch: a non-null collection must still expand, so passing the
        // pre-parsed names through for the null case cannot have leaked into the expanded case.
        var command = new FakeCommand { CommandText = "SELECT * FROM T WHERE A = @A AND Id IN @Ids" };

        ParameterBinder.Bind(command, new NullableCollectionParams { A = 1, Ids = [10, 20, 30] });

        // Three expanded collection elements plus the scalar A.
        Assert.Equal(4, command.Parameters.Count);
        Assert.Contains("IN (@Ids0, @Ids1, @Ids2)", command.CommandText);
    }

    [Fact]
    public void Bind_CollectionPropertyNullThenPopulated_ExpandsOnTheSecondCall()
    {
        // The reason this shape is not cached at all: the same (sql, type, commandType) key must
        // still expand later. Pinning it here so the B3-3 change cannot quietly enable caching.
        const string Sql = "SELECT * FROM T WHERE A = @A AND Id IN @Ids";

        var first = new FakeCommand { CommandText = Sql };
        ParameterBinder.Bind(first, new NullableCollectionParams { A = 1, Ids = null });

        var second = new FakeCommand { CommandText = Sql };
        ParameterBinder.Bind(second, new NullableCollectionParams { A = 1, Ids = [1, 2] });

        // Two expanded elements plus A - the null first call must not have cached a scalar template
        // that permanently defeats expansion.
        Assert.Equal(3, second.Parameters.Count);
        Assert.Contains("IN (@Ids0, @Ids1)", second.CommandText);
    }

    // ------------------------------------------------------------------
    // B3-5: nullable enums were already handled by the IsEnum branch
    // ------------------------------------------------------------------

    [Fact]
    public void ApplyTypeHandlerIfNeeded_NullableEnumValue_BoxesAsTheUnderlyingEnum()
    {
        // The premise of the removed branch being dead: boxing a SomeEnum? yields a box of SomeEnum,
        // so GetType() can never return Nullable<SomeEnum> and the generic branch was unreachable.
        Sample? nullable = Sample.Second;
        object boxed = nullable!;

        Assert.Equal(typeof(Sample), boxed.GetType());
        Assert.False(boxed.GetType().IsGenericType);
    }

    [Fact]
    public void ApplyTypeHandlerIfNeeded_NullableEnumWithStringStorage_StillConvertsToItsName()
    {
        // Behaviour the removed branch appeared to provide, delivered by the IsEnum branch.
        Sample? nullable = Sample.Second;
        PropertyInfo property = typeof(StringStoredEnumHolder).GetProperty(nameof(StringStoredEnumHolder.Value))!;

        Assert.Equal("Second", ParameterBinder.ApplyTypeHandlerIfNeeded(nullable, property));
    }

    [Fact]
    public void ApplyTypeHandlerIfNeeded_NullValue_IsReturnedUnchanged()
    {
        Assert.Null(ParameterBinder.ApplyTypeHandlerIfNeeded(null, propertyInfo: null));
    }

    // ------------------------------------------------------------------
    // Fixtures
    // ------------------------------------------------------------------

    private enum Sample
    {
        First = 0,
        Second = 1,
    }

    private class StringStoredEnumHolder
    {
        [EnumStorage(Attributes.EnumStorage.String)]
        public Sample? Value { get; set; }
    }

    private class NullableCollectionParams
    {
        public int A { get; set; }
        public int[]? Ids { get; set; }
    }

    private sealed class FakeCommand : IDbCommand
    {
        public string CommandText { get; set; } = "";
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters { get; } = new FakeParameterCollection();
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => new FakeParameter();
        public void Dispose() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => throw new NotSupportedException();
        public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();
        public object? ExecuteScalar() => null;
        public void Prepare() { }
    }

    private sealed class FakeParameter : IDbDataParameter
    {
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => true;
        public string ParameterName { get; set; } = "";
        public string SourceColumn { get; set; } = "";
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
    }

    private sealed class FakeParameterCollection : List<object>, IDataParameterCollection
    {
        public object this[string parameterName]
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public bool Contains(string parameterName) => false;
        public int IndexOf(string parameterName) => -1;
        public void RemoveAt(string parameterName) { }
    }
}
