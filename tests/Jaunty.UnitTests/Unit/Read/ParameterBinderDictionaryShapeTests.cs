using System.Collections.Generic;
using System.Data.SQLite;

using Jaunty.Core;

using Xunit;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R35-057. <c>ParameterBinder.Bind</c> tested <c>parameters is IDictionary&lt;string, object?&gt;</c>
/// and nothing else, while the generator's <c>ParameterRooting.IsDictionaryShape</c> - which decides
/// whether to emit rooting and whether to suppress the JAUNTYGEN003 "the reflection binder cannot see
/// through this" warning - answered yes for <c>IReadOnlyDictionary&lt;,&gt;</c> and for any
/// <c>IDictionary&lt;,&gt;</c> whatever its type arguments. <c>TValue</c> is invariant on
/// <c>IDictionary&lt;,&gt;</c>, so a <c>Dictionary&lt;string, int&gt;</c> is not an
/// <c>IDictionary&lt;string, object?&gt;</c>: it fell past that test, past <c>IsScalarType</c>, and
/// into <c>ParameterCache.Get</c>, which reflected over <c>Count</c>/<c>Keys</c>/<c>Values</c>/
/// <c>Comparer</c> and threw - with the build-time warning suppressed.
/// <para>
/// The two predicates could not both be right. The runtime is the side that moved, because widening
/// it makes these calls work rather than merely reporting the failure earlier.
/// </para>
/// </summary>
public class ParameterBinderDictionaryShapeTests
{
    private static SQLiteConnection Open()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE dict_rows (id INTEGER PRIMARY KEY, name TEXT); " +
                          "INSERT INTO dict_rows (id, name) VALUES (1, 'Ada'), (2, 'Grace')";
        cmd.ExecuteNonQuery();

        return connection;
    }

    internal sealed class DictRow
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// The shape that used to throw: same keys, narrower value type.
    /// </summary>
    [Fact]
    public void ADictionaryOfStringToInt_BindsByKey()
    {
        using var connection = Open();
        var parameters = new Dictionary<string, int> { ["Id"] = 2 };

        var rows = connection.Query<DictRow>("SELECT id AS Id, name AS Name FROM dict_rows WHERE id = @Id", parameters);

        Assert.Single(rows);
        Assert.Equal("Grace", rows[0].Name);
    }

    [Fact]
    public void ADictionaryOfStringToString_BindsByKey()
    {
        using var connection = Open();
        var parameters = new Dictionary<string, string> { ["Name"] = "Ada" };

        var rows = connection.Query<DictRow>("SELECT id AS Id, name AS Name FROM dict_rows WHERE name = @Name", parameters);

        Assert.Single(rows);
        Assert.Equal(1L, rows[0].Id);
    }

    /// <summary>
    /// <c>IReadOnlyDictionary&lt;string, object?&gt;</c> is not an <c>IDictionary&lt;string, object?&gt;</c>
    /// either, and the generator claimed it too.
    /// </summary>
    [Fact]
    public void AReadOnlyDictionary_BindsByKey()
    {
        using var connection = Open();
        IReadOnlyDictionary<string, object?> parameters = new Dictionary<string, object?> { ["Id"] = 1 };

        var rows = connection.Query<DictRow>("SELECT id AS Id, name AS Name FROM dict_rows WHERE id = @Id", parameters);

        Assert.Single(rows);
        Assert.Equal("Ada", rows[0].Name);
    }

    /// <summary>
    /// A sorted dictionary is neither of the two interfaces the binder used to test for, and reaches
    /// the widened path through the non-generic <c>IDictionary</c>.
    /// </summary>
    [Fact]
    public void ASortedDictionary_BindsByKey()
    {
        using var connection = Open();
        var parameters = new SortedDictionary<string, long> { ["Id"] = 2 };

        var rows = connection.Query<DictRow>("SELECT id AS Id, name AS Name FROM dict_rows WHERE id = @Id", parameters);

        Assert.Single(rows);
        Assert.Equal("Grace", rows[0].Name);
    }

    /// <summary>
    /// The strictness that applies to <c>Dictionary&lt;string, object?&gt;</c> applies to the widened
    /// shapes as well - a widening that lost the unused-key and missing-key checks would be a
    /// different contract, not the same one for more types.
    /// </summary>
    [Fact]
    public void AMissingKey_StillThrows()
    {
        using var connection = Open();
        var parameters = new Dictionary<string, int> { ["Other"] = 2 };

        var ex = Assert.Throws<ArgumentException>(() =>
            connection.Query<DictRow>("SELECT id AS Id, name AS Name FROM dict_rows WHERE id = @Id", parameters));

        Assert.Contains("No value found in dictionary", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnusedKey_StillThrows()
    {
        using var connection = Open();
        var parameters = new Dictionary<string, int> { ["Id"] = 2, ["Unused"] = 9 };

        var ex = Assert.Throws<ArgumentException>(() =>
            connection.Query<DictRow>("SELECT id AS Id, name AS Name FROM dict_rows WHERE id = @Id", parameters));

        Assert.Contains("Unused parameter keys", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A non-string-keyed dictionary is not a dictionary of named values at all, so it must keep
    /// falling through to the property path rather than being half-bound.
    /// </summary>
    [Fact]
    public void ANonStringKeyedDictionary_IsNotBoundByKey()
    {
        using var connection = Open();
        var parameters = new Dictionary<int, string> { [1] = "Ada" };

        Assert.ThrowsAny<Exception>(() =>
            connection.Query<DictRow>("SELECT id AS Id, name AS Name FROM dict_rows WHERE id = @Id", parameters));
    }

    /// <summary>
    /// The control: the shape that always worked still does, and an ordinary properties object is
    /// untouched by the widening.
    /// </summary>
    [Fact]
    public void TheOriginalShapesStillBind()
    {
        using var connection = Open();

        var byDictionary = connection.Query<DictRow>(
            "SELECT id AS Id, name AS Name FROM dict_rows WHERE id = @Id",
            new Dictionary<string, object?> { ["Id"] = 1 });

        var byObject = connection.Query<DictRow>(
            "SELECT id AS Id, name AS Name FROM dict_rows WHERE id = @Id",
            new { Id = 1 });

        Assert.Equal("Ada", Assert.Single(byDictionary).Name);
        Assert.Equal("Ada", Assert.Single(byObject).Name);
    }
}
