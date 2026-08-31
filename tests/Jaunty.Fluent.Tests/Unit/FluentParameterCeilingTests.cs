using System.Data;
using System.Data.SQLite;

using Jaunty.Dialects;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit;

/// <summary>
/// AUD-R26 (batch 5, medium/consistency). The <c>MaxParametersPerStatement</c> guard fired on
/// exactly one of four routes to the same <c>IN</c> list, and Fluent was not that route.
/// <c>ParameterBinder.ExpandCollectionParameters</c> throws when a collection-valued member expands
/// past the dialect ceiling, but Fluent never reaches it: <c>WhereExpressionVisitor</c>'s
/// <c>Contains</c> handler and <c>QueryBuilder.BuildInClause</c> both expand the collection
/// themselves into individually-named parameters, which arrive at the provider as ordinary scalars.
///
/// <para>
/// The finding's measurement, at n=1200 on Microsoft.Data.Sqlite: core's
/// <c>Query("... IN @ids", new { ids })</c> threw "exceeding ... 999" while
/// <c>.Where(ids.Contains(p.Id))</c>, <c>.WhereIn(p =&gt; p.Id, ids)</c> and a hand-written
/// placeholder list all executed. Both halves were wrong in opposite directions - the ceiling that
/// was enforced was 33x too low, and the routes that would have benefited from a real one checked
/// nothing. A caller who hit the core error and rewrote to the fluent form to work around it got no
/// error and no batching.
/// </para>
///
/// <para>
/// These tests fix the consistency half. The ceiling half is in
/// <c>Jaunty.Tests/Unit/Dialects/SqliteParameterCeilingTests.cs</c>.
/// </para>
/// </summary>
public class FluentParameterCeilingTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentParameterCeilingTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    private static int[] Ids(int count)
    {
        var ids = new int[count];
        for (int i = 0; i < count; i++)
            ids[i] = i + 1;
        return ids;
    }

    /// <summary>
    /// The Fluent suite runs on System.Data.SQLite while core's runs on Microsoft.Data.Sqlite, and
    /// both resolve to the same <see cref="SQLiteDialect"/> - so the corrected ceiling has to be
    /// right for <em>this</em> native build too, not just the one it was measured against. Asserted
    /// by executing it rather than assumed.
    /// </summary>
    [Fact]
    public void TheCorrectedCeilingHoldsOnThisProviderToo()
    {
        int ceiling = new SQLiteDialect().MaxParametersPerStatement;

        using var command = (SQLiteCommand)_fixture.Connection.CreateCommand();
        var names = new string[ceiling];
        for (int i = 0; i < ceiling; i++)
        {
            names[i] = "@p" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            command.Parameters.AddWithValue(names[i], i);
        }

        command.CommandText = "SELECT 1 WHERE 1 IN (" + string.Join(",", names) + ")";
        command.ExecuteScalar();
    }

    // ------------------------------------------------------------------
    // The defect: a list core refused, Fluent executed
    // ------------------------------------------------------------------

    /// <summary>
    /// 1,200 values is the finding's own n. It is comfortably under the real ceiling, so after the
    /// fix every route accepts it - the point being that they now agree.
    /// </summary>
    [Fact]
    public void AtTwelveHundredValuesEveryRouteAgreesItIsFine()
    {
        int[] ids = Ids(1200);

        List<Product> viaContains = _fixture.Connection.From<Product>()
            .Where(p => ids.Contains(p.ProductId))
            .Select()
            .ToList();

        List<Product> viaWhereIn = _fixture.Connection.From<Product>()
            .WhereIn(p => p.ProductId, ids)
            .Select()
            .ToList();

        List<Product> viaCore = _fixture.Connection
            .Query<Product>("SELECT * FROM products WHERE product_id IN @Ids", new { Ids = ids })
            .ToList();

        Assert.Equal(viaCore.Count, viaContains.Count);
        Assert.Equal(viaCore.Count, viaWhereIn.Count);
    }

    /// <summary>
    /// Past the real ceiling, the two Fluent routes now refuse in Jaunty's own words instead of
    /// handing the provider a statement it cannot run. This is the behaviour that was missing.
    /// </summary>
    [Fact]
    public void PastTheCeilingTheContainsRouteRefusesWithJauntysMessage()
    {
        int[] ids = Ids(40000);

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => _fixture.Connection.From<Product>()
                .Where(p => ids.Contains(p.ProductId))
                .Select()
                .ToList());

        Assert.Contains("32766", refused.Message, StringComparison.Ordinal);
        Assert.Contains("batching", refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PastTheCeilingTheWhereInRouteRefusesWithJauntysMessage()
    {
        int[] ids = Ids(40000);

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => _fixture.Connection.From<Product>()
                .WhereIn(p => p.ProductId, ids)
                .Select()
                .ToList());

        Assert.Contains("32766", refused.Message, StringComparison.Ordinal);
        Assert.Contains("batching", refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Both Fluent routes and core now produce the <em>same</em> message for the same mistake. The
    /// finding's complaint was not only that Fluent didn't check - it was that a caller moving
    /// between the two APIs got contradictory behaviour with no way to predict which.
    /// </summary>
    [Fact]
    public void AllThreeRoutesGiveTheSameMessageForTheSameMistake()
    {
        int[] ids = Ids(40000);

        string fromContains = Assert.Throws<InvalidOperationException>(
            () => _fixture.Connection.From<Product>().Where(p => ids.Contains(p.ProductId)).Select().ToList()).Message;

        string fromWhereIn = Assert.Throws<InvalidOperationException>(
            () => _fixture.Connection.From<Product>().WhereIn(p => p.ProductId, ids).Select().ToList()).Message;

        string fromCore = Assert.Throws<InvalidOperationException>(
            () => _fixture.Connection.Query<Product>(
                "SELECT * FROM products WHERE product_id IN @Ids", new { Ids = ids }).ToList()).Message;

        // The provider name differs by design - it names what imposed the limit - so compare the
        // parts that describe the problem and the way out.
        foreach (string message in new[] { fromContains, fromWhereIn, fromCore })
        {
            Assert.Contains("40000 parameters", message, StringComparison.Ordinal);
            Assert.Contains("maximum of 32766 parameters", message, StringComparison.Ordinal);
            Assert.Contains("Consider batching the query into smaller chunks.", message, StringComparison.Ordinal);
        }
    }

    /// <summary>An empty collection still short-circuits rather than being counted.</summary>
    [Fact]
    public void AnEmptyCollectionIsUnaffected()
    {
        List<Product> none = _fixture.Connection.From<Product>()
            .WhereIn(p => p.ProductId, Array.Empty<int>())
            .Select()
            .ToList();

        Assert.Empty(none);
    }
}
