using System.Data.SQLite;

using Jaunty.Attributes;

using Xunit;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R35-048. Every <c>Query</c>/<c>QueryFirst</c>/<c>QuerySingle</c>/<c>QueryStream</c> overload
/// in <c>QueryMultiEntity.cs</c> claimed "strict mapping mode - all public writable properties on
/// both T1 and T2 must have matching columns in the result set" and carried an
/// <see cref="InvalidOperationException"/> tag for a property with no matching column. That
/// exception cannot be thrown on this path: <c>QueryMultiEntityCore</c> ignores the
/// <c>MappingMode</c> argument unless <c>options.Mapper</c> is set and builds both setter arrays in
/// projection mode with no completeness check afterwards.
///
/// <para>
/// These tests assert the behaviour the corrected docs now describe. They are the reason the doc
/// change is safe: if a strict mode were ever introduced here, they fail rather than the docs
/// quietly going stale again.
/// </para>
/// </summary>
public class MultiEntityProjectionModeTests
{
    [Table("orders")]
    private sealed class ProjOrder
    {
        [Key]
        public int OrderId { get; set; }

        public string? OrderRef { get; set; }

        /// <summary>Never in any SELECT list below.</summary>
        public string? NotSelected { get; set; }
    }

    [Table("customers")]
    private sealed class ProjCustomer
    {
        [Key]
        public int CustomerId { get; set; }

        public string? CustomerName { get; set; }

        /// <summary>Never in any SELECT list below.</summary>
        public int AlsoNotSelected { get; set; }
    }

    private const string Sql =
        "SELECT 1 AS OrderId, 'A-1' AS OrderRef, 7 AS CustomerId, 'Ada' AS CustomerName";

    private static SQLiteConnection Open()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    /// <summary>
    /// The claim the docs made: a property with no column throws. It does not.
    /// </summary>
    [Fact]
    public void AMissingColumnDoesNotThrow()
    {
        using var connection = Open();

        var rows = connection.Query<ProjOrder, ProjCustomer>(Sql);

        Assert.Single(rows);
    }

    /// <summary>
    /// What actually happens instead: the property keeps its default, silently.
    /// </summary>
    [Fact]
    public void AMissingColumnLeavesThePropertyAtItsDefault()
    {
        using var connection = Open();

        (ProjOrder order, ProjCustomer customer) = connection.Query<ProjOrder, ProjCustomer>(Sql)[0];

        Assert.Null(order.NotSelected);
        Assert.Equal(0, customer.AlsoNotSelected);
    }

    /// <summary>
    /// The columns that are present still map, on both entities - so the test above is measuring a
    /// missing column rather than a mapper that did nothing at all.
    /// </summary>
    [Fact]
    public void ThePresentColumnsStillMap()
    {
        using var connection = Open();

        (ProjOrder order, ProjCustomer customer) = connection.Query<ProjOrder, ProjCustomer>(Sql)[0];

        Assert.Equal(1, order.OrderId);
        Assert.Equal("A-1", order.OrderRef);
        Assert.Equal(7, customer.CustomerId);
        Assert.Equal("Ada", customer.CustomerName);
    }

    /// <summary>
    /// A misspelled column is the same case as a missing one, and the one a caller is likelier to
    /// hit: the value goes to no property at all rather than to the one it was meant for.
    /// </summary>
    [Fact]
    public void AMisspelledColumnIsNotReported()
    {
        using var connection = Open();

        (ProjOrder order, ProjCustomer _) = connection.Query<ProjOrder, ProjCustomer>(
            "SELECT 1 AS OrderId, 'A-1' AS OrderReff, 7 AS CustomerId, 'Ada' AS CustomerName")[0];

        Assert.Null(order.OrderRef);
    }

    [Fact]
    public void QueryFirstBehavesTheSameWay()
    {
        using var connection = Open();

        (ProjOrder order, ProjCustomer customer) = connection.QueryFirst<ProjOrder, ProjCustomer>(Sql);

        Assert.Null(order.NotSelected);
        Assert.Equal(0, customer.AlsoNotSelected);
    }

    [Fact]
    public void QuerySingleBehavesTheSameWay()
    {
        using var connection = Open();

        (ProjOrder order, ProjCustomer customer) = connection.QuerySingle<ProjOrder, ProjCustomer>(Sql);

        Assert.Null(order.NotSelected);
        Assert.Equal(0, customer.AlsoNotSelected);
    }

    [Fact]
    public async Task TheAsyncTwinBehavesTheSameWay()
    {
        using var connection = Open();

        var rows = await connection.QueryAsync<ProjOrder, ProjCustomer>(Sql);

        Assert.Null(rows[0].Item1.NotSelected);
        Assert.Equal(0, rows[0].Item2.AlsoNotSelected);
    }
}
