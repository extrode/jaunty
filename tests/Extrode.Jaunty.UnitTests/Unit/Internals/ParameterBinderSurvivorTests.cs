using System.Collections;
using System.Data;

using Extrode.Jaunty.Internals.Parameters;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Internals;

[Collection(ParameterBinderCacheCollection.Name)]
public class ParameterBinderSurvivorTests
{
    public sealed class TwoProps
    {
        public int A { get; set; }
        public int B { get; set; }
    }

    public sealed class ThreeProps
    {
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
    }

    public sealed class ListAndScalars
    {
        public IEnumerable<int>? Ids { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }

    public sealed class TwoNullLists
    {
        public int[]? A { get; set; }
        public int[]? B { get; set; }
    }

    public sealed class Tagged
    {
        public int Id { get; set; }
        public IEnumerable? Tags { get; set; }
    }

    public sealed class IntKeyedDictionary : IDictionary
    {
        public int Id { get; set; }

        int ICollection.Count => 1;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;
        bool IDictionary.IsFixedSize => true;
        bool IDictionary.IsReadOnly => true;
        ICollection IDictionary.Keys => new[] { 1 };
        ICollection IDictionary.Values => new[] { 2 };
        object? IDictionary.this[object key]
        {
            get => 2;
            set => throw new NotSupportedException();
        }

        void IDictionary.Add(object key, object? value) => throw new NotSupportedException();
        void IDictionary.Clear() => throw new NotSupportedException();
        bool IDictionary.Contains(object key) => false;
        void IDictionary.Remove(object key) => throw new NotSupportedException();
        void ICollection.CopyTo(Array array, int index) => throw new NotSupportedException();
        IDictionaryEnumerator IDictionary.GetEnumerator() => new Hashtable { [1] = 2 }.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IDictionary)this).GetEnumerator();
    }

    public sealed class OneProp
    {
        public int A { get; set; }
    }

    public ParameterBinderSurvivorTests()
    {
        var shapes = typeof(ParameterBinder).GetField("ShapeCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!;
        ((IDictionary)shapes).Clear();
    }

    private static string Unique() => Guid.NewGuid().ToString("N");

    private static FakeCommand Command(string sql) => new() { CommandText = sql };

    private static string Message(Action act) => Assert.Throws<ArgumentException>(act).Message;

    [Fact]
    public void AMissingSqlParameter_ListsEveryPropertyOfTheType()
        => Assert.Equal(
            "No property found on type 'TwoProps' matching SQL parameter '@Missing'. Available properties: A, B",
            Message(() => ParameterBinder.Bind(Command("x = @Missing -- " + Unique()), new TwoProps())));

    [Fact]
    public void AMissingSqlParameterBesideACollection_ListsEveryPropertyOfTheType()
        => Assert.Equal(
            "No property found on type 'ListAndScalars' matching SQL parameter '@Missing'. Available properties: Ids, X, Y",
            Message(() => ParameterBinder.Bind(Command("x IN @Ids AND y = @Missing -- " + Unique()), new ListAndScalars { Ids = [1, 2] })));

    [Fact]
    public void UnusedProperties_AreListedTogether()
        => Assert.Equal(
            "Unused parameter properties on type 'ThreeProps': B, C. SQL contains no matching parameters.",
            Message(() => ParameterBinder.Bind(Command("x = @A -- " + Unique()), new ThreeProps())));

    [Fact]
    public void UnusedPropertiesBesideACollection_AreListedTogether()
        => Assert.Equal(
            "Unused parameter properties on type 'ListAndScalars': X, Y. SQL contains no matching parameters.",
            Message(() => ParameterBinder.Bind(Command("x IN @Ids -- " + Unique()), new ListAndScalars { Ids = [1] })));

    [Fact]
    public void AScalarAgainstTwoParameters_NamesBothAndTheArgument()
    {
        var ex = Assert.Throws<ArgumentException>(() => ParameterBinder.Bind(Command("x = @A AND y = @B -- " + Unique()), 5));

        Assert.Equal("value", ex.ParamName);
        Assert.StartsWith("A single scalar parameter value cannot be bound to SQL containing 2 distinct parameters (A, B). Pass an object or dictionary", ex.Message);
    }

    [Fact]
    public void ADictionaryMissingAParameter_NamesItAndTheArgument()
    {
        var ex = Assert.Throws<ArgumentException>(() => ParameterBinder.Bind(
            Command("x = @A AND y = @B -- " + Unique()),
            new Dictionary<string, object?> { ["A"] = 1 }));

        Assert.Equal("parameters", ex.ParamName);
        Assert.StartsWith("No value found in dictionary for SQL parameter '@B'.", ex.Message);
    }

    [Fact]
    public void UnusedDictionaryKeys_AreListedTogetherAgainstTheArgument()
    {
        var ex = Assert.Throws<ArgumentException>(() => ParameterBinder.Bind(
            Command("x = @A -- " + Unique()),
            new Dictionary<string, object?> { ["A"] = 1, ["X"] = 2, ["Y"] = 3 }));

        Assert.Equal("parameters", ex.ParamName);
        Assert.StartsWith("Unused parameter keys in dictionary: X, Y. SQL contains no matching parameters.", ex.Message);
    }

    [Fact]
    public void ADictionaryKeyDifferingOnlyInCase_BindsWithoutBeingUnused()
    {
        var command = Command("x = @A -- " + Unique());

        ParameterBinder.Bind(command, new Dictionary<string, object?> { ["a"] = 1 });

        var parameter = Assert.IsType<FakeParameter>(Assert.Single(command.Parameters));
        Assert.Equal("A", parameter.ParameterName);
        Assert.Equal(1, parameter.Value);
    }

    [Fact]
    public void AStoredProcedureDictionary_BindsEveryEntryWithNullAsDbNull()
    {
        var command = new FakeCommand { CommandType = CommandType.StoredProcedure, CommandText = "Proc" };

        ParameterBinder.Bind(command, new Dictionary<string, object?> { ["a"] = 5, ["b"] = null });

        var bound = command.Parameters.Cast<FakeParameter>().ToArray();
        Assert.Equal(["a", "b"], bound.Select(p => p.ParameterName));
        Assert.Equal(5, bound[0].Value);
        Assert.Same(DBNull.Value, bound[1].Value);
    }

    [Fact]
    public void TwoNullCollections_AreBothExpandedAndNeitherBoundAsAParameter()
    {
        var command = Command("x IN @A AND y IN @B -- " + Unique());

        ParameterBinder.Bind(command, new TwoNullLists());

        Assert.StartsWith("x IN (SELECT NULL WHERE 1 = 0) AND y IN (SELECT NULL WHERE 1 = 0)", command.CommandText, StringComparison.Ordinal);
        Assert.Empty(command.Parameters);
    }

    [Fact]
    public void ACollectionTypedPropertyHoldingAScalarOnce_StillExpandsWhenACollectionArrivesLater()
    {
        string sql = "x = @Id AND y IN @Tags -- " + Unique();

        ParameterBinder.Bind(Command(sql), new Tagged { Id = 1, Tags = "abc" });
        var second = Command(sql);
        ParameterBinder.Bind(second, new Tagged { Id = 1, Tags = new[] { 1, 2 } });

        Assert.Contains("@Tags0", second.CommandText, StringComparison.Ordinal);
    }

    [Fact]
    public void TryRebind_ACollectionHoldingANonParameter_IsRefused()
    {
        string sql = "x = @A -- " + Unique();
        ParameterBinder.Bind(Command(sql), new OneProp { A = 1 });
        var command = Command(sql);
        command.Parameters.Add("junk");

        Assert.False(ParameterBinder.TryRebind(command, new OneProp { A = 2 }));
    }

    [Fact]
    public void TryRebind_ADictionaryShapeBoundThroughItsProperties_IsRefused()
    {
        string sql = "x = @Id -- " + Unique();
        var parameters = new IntKeyedDictionary { Id = 1 };
        ParameterBinder.Bind(Command(sql), parameters);
        var command = Command(sql);
        ParameterBinder.Bind(command, parameters);

        Assert.False(ParameterBinder.TryRebind(command, parameters));
    }

    public sealed class ReadOnlyNamedValues : IReadOnlyDictionary<string, object?>
    {
        public object? this[string key] => 1;
        public IEnumerable<string> Keys => ["Id"];
        public IEnumerable<object?> Values => [1];
        public int Count => 1;
        public bool ContainsKey(string key) => key == "Id";
        public bool TryGetValue(string key, out object? value)
        {
            value = 1;
            return true;
        }

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            yield return new KeyValuePair<string, object?>("Id", 1);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public sealed class SetOnlyAndReadable
    {
        public int Id { get; set; }
        public string WriteOnly { set { } }
    }

    [Fact]
    public void ACollectionTypedShapeHoldingAScalar_StillBindsEveryParameter()
    {
        var command = Command("x = @Id AND y IN @Tags -- " + Unique());

        ParameterBinder.Bind(command, new Tagged { Id = 4, Tags = "abc" });

        var bound = command.Parameters.Cast<FakeParameter>().ToDictionary(p => p.ParameterName, p => p.Value);
        Assert.Equal(4, bound["Id"]);
        Assert.Equal("abc", bound["Tags"]);
    }

    [Fact]
    public void ACollectionTypedShape_IsNeverCachedAsATemplate()
    {
        var command = Command("x = @Id AND y IN @Tags -- " + Unique());
        ParameterBinder.Bind(command, new Tagged { Id = 1, Tags = "abc" });

        Assert.False(ParameterBinder.TryRebind(command, new Tagged { Id = 2, Tags = "abc" }));
    }

    [Fact]
    public void AScalarOnlyShape_IsCachedAsATemplateAndRebinds()
    {
        var command = Command("x = @A -- " + Unique());
        ParameterBinder.Bind(command, new OneProp { A = 1 });

        Assert.True(ParameterBinder.TryRebind(command, new OneProp { A = 2 }));
        Assert.Equal(2, Assert.IsType<FakeParameter>(Assert.Single(command.Parameters)).Value);
    }

    [Fact]
    public void TryRebind_AReadOnlyNamedValueDictionary_IsRefusedEvenWithACachedTemplate()
    {
        string sql = "x = @A -- " + Unique();
        var command = Command(sql);
        ParameterBinder.Bind(command, new OneProp { A = 1 });

        var cache = typeof(ParameterBinder).GetField("TemplateCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!;
        const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var args = new object?[] { (sql, typeof(OneProp), command.GetType()), null };
        Assert.True((bool)cache.GetType().GetMethod("TryGetValue", Any)!.Invoke(cache, args)!);
        cache.GetType().GetMethod("TryAdd", Any)!.Invoke(cache, [(sql, typeof(ReadOnlyNamedValues), command.GetType()), args[1]]);

        Assert.False(ParameterBinder.TryRebind(command, new ReadOnlyNamedValues()));
    }

    [Fact]
    public void ASetOnlyProperty_IsNotAParameter()
    {
        var cache = typeof(ParameterCache).GetField("Cache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!;
        ((IDictionary)cache).Clear();

        Assert.Equal(["Id"], ParameterCache.Get(typeof(SetOnlyAndReadable)).Select(m => m.Name));
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

        public bool Contains(string parameterName) => throw new NotSupportedException();
        public int IndexOf(string parameterName) => throw new NotSupportedException();
        public void RemoveAt(string parameterName) => throw new NotSupportedException();
    }
}
