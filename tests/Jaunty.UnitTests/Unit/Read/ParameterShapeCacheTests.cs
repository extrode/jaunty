using Jaunty.Internals.Parameters;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R35-116. The name lookup and the collection-typed answer are now built once per parameters
/// type and shared across binds instead of rebuilt per call. These pin what sharing could break:
/// one type's shape leaking into another's, and a shape going stale between calls.
/// </summary>
public class ParameterShapeCacheTests
{
    private sealed class ShapeA
    {
        public int Id { get; set; }
        public int[]? Values { get; set; }
    }

    private sealed class ShapeB
    {
        public int Id { get; set; }
        public string? Values { get; set; }
    }

    [Fact]
    public void TwoTypesSharingAPropertyName_KeepTheirOwnShapes()
    {
        const string sql = "SELECT * FROM t WHERE id = @Id AND v IN @Values";

        var a = new MockDbCommand(sql);
        ParameterBinder.Bind(a, new ShapeA { Id = 1, Values = [10, 20] });

        var b = new MockDbCommand("SELECT * FROM t WHERE id = @Id AND v = @Values");
        ParameterBinder.Bind(b, new ShapeB { Id = 2, Values = "ten" });

        Assert.Contains("(@Values0, @Values1)", a.CommandText);
        Assert.Equal(3, a.Parameters.Count);
        Assert.Equal(2, b.Parameters.Count);
        Assert.Equal("ten", b.Parameters[1].Value);
    }

    [Fact]
    public void TheCachedShape_StillExpandsOnEveryCall_NotJustTheFirst()
    {
        const string sql = "SELECT * FROM t WHERE id = @Id AND v IN @Values";

        for (int i = 0; i < 3; i++)
        {
            var command = new MockDbCommand(sql);
            ParameterBinder.Bind(command, new ShapeA { Id = i, Values = [i, i + 1] });

            Assert.Contains("(@Values0, @Values1)", command.CommandText);
            Assert.Equal(3, command.Parameters.Count);
            Assert.Equal(i, command.Parameters[0].Value);
            Assert.Equal(i, command.Parameters[1].Value);
        }
    }

    private sealed class StringAsCharSequence
    {
        public IEnumerable<char>? Values { get; set; }
    }

    // The collection-typed-property guard is what keeps a shape that *can* expand out of the
    // template cache even on a call that did not expand. A property declared IEnumerable<char> is
    // collection-typed, but a string value is excluded from expansion, so this call produces no
    // expansion while a later call with a real char collection must still expand. Caching a scalar
    // template on the first call would defeat the second permanently.
    [Fact]
    public void ACollectionTypedPropertyHoldingANonExpandableValue_DoesNotCacheAScalarTemplate()
    {
        const string sql = "SELECT * FROM t WHERE v IN @Values";

        var first = new MockDbCommand(sql);
        ParameterBinder.Bind(first, new StringAsCharSequence { Values = "ab" });
        Assert.DoesNotContain("(@Values", first.CommandText);

        var second = new MockDbCommand(sql);
        ParameterBinder.Bind(second, new StringAsCharSequence { Values = ['a', 'b'] });

        Assert.Contains("(@Values0, @Values1)", second.CommandText);
        Assert.Equal(2, second.Parameters.Count);
    }

    [Fact]
    public void TheCachedLookup_StaysCaseInsensitive_AcrossRepeatedBinds()
    {
        const string sql = "SELECT * FROM t WHERE id = @id AND v IN @values";

        for (int i = 0; i < 2; i++)
        {
            var command = new MockDbCommand(sql);
            ParameterBinder.Bind(command, new ShapeA { Id = 7, Values = [1] });

            Assert.Equal(2, command.Parameters.Count);
            Assert.Equal(7, command.Parameters[0].Value);
        }
    }
}
