using System.Data;

using Jaunty.Core;

using Xunit;

namespace Jaunty.Tests.Unit.Core;

/// <summary>
/// AUD-R26 (batch 4, low/consistency). <c>MultiEntityCommandOptions&lt;T1..Tn&gt;</c> - all six
/// arities - carried <c>Transaction</c>, <c>CommandTimeout</c> and <c>CommandType</c> and nothing
/// else, so its implicit conversion to <c>CommandOptions&lt;(T1, ..., Tn)&gt;</c> always produced a
/// null <c>ExpectedRowCount</c>.
///
/// <para>
/// That is not a hint nobody reads: <c>QueryCore</c> and <c>QueryCoreAsync</c> size their result
/// list with <c>options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity</c> at all twelve
/// multi-entity call sites. The plumbing to honour the hint was already in place at the far end -
/// the near end simply had no field to put a value in, so no multi-entity query could ever pre-size
/// its list no matter what the caller asked for.
/// </para>
///
/// <para>
/// These tests assert on the conversion rather than on an allocated list's capacity, because
/// capacity is not observable through the public query API and <c>List&lt;T&gt;</c> is free to grow
/// past whatever it starts with. The conversion is where the value was being dropped, and it is the
/// only thing between the caller and a consumer that already works.
/// </para>
/// </summary>
public class MultiEntityExpectedRowCountTests
{
    private class A { public int Id { get; set; } }
    private class B { public int Id { get; set; } }
    private class C { public int Id { get; set; } }
    private class D { public int Id { get; set; } }
    private class E { public int Id { get; set; } }
    private class F { public int Id { get; set; } }
    private class G { public int Id { get; set; } }

    [Fact]
    public void Arity2_CarriesExpectedRowCountThroughTheConversion()
    {
        var options = new MultiEntityCommandOptions<A, B>(expectedRowCount: 10_000);

        CommandOptions<(A, B)> converted = options;

        Assert.Equal(10_000, converted.ExpectedRowCount);
    }

    [Fact]
    public void Arity3_CarriesExpectedRowCountThroughTheConversion()
    {
        var options = new MultiEntityCommandOptions<A, B, C>(expectedRowCount: 10_000);

        CommandOptions<(A, B, C)> converted = options;

        Assert.Equal(10_000, converted.ExpectedRowCount);
    }

    [Fact]
    public void Arity4_CarriesExpectedRowCountThroughTheConversion()
    {
        var options = new MultiEntityCommandOptions<A, B, C, D>(expectedRowCount: 10_000);

        CommandOptions<(A, B, C, D)> converted = options;

        Assert.Equal(10_000, converted.ExpectedRowCount);
    }

    [Fact]
    public void Arity5_CarriesExpectedRowCountThroughTheConversion()
    {
        var options = new MultiEntityCommandOptions<A, B, C, D, E>(expectedRowCount: 10_000);

        CommandOptions<(A, B, C, D, E)> converted = options;

        Assert.Equal(10_000, converted.ExpectedRowCount);
    }

    [Fact]
    public void Arity6_CarriesExpectedRowCountThroughTheConversion()
    {
        var options = new MultiEntityCommandOptions<A, B, C, D, E, F>(expectedRowCount: 10_000);

        CommandOptions<(A, B, C, D, E, F)> converted = options;

        Assert.Equal(10_000, converted.ExpectedRowCount);
    }

    [Fact]
    public void Arity7_CarriesExpectedRowCountThroughTheConversion()
    {
        var options = new MultiEntityCommandOptions<A, B, C, D, E, F, G>(expectedRowCount: 10_000);

        CommandOptions<(A, B, C, D, E, F, G)> converted = options;

        Assert.Equal(10_000, converted.ExpectedRowCount);
    }

    /// <summary>
    /// The three fields that already worked must keep working, and the new parameter is appended
    /// last with a default so no existing positional call site changes meaning.
    /// </summary>
    [Fact]
    public void TheExistingFieldsAreStillCarried()
    {
        var transaction = new StubTransaction();
        var options = new MultiEntityCommandOptions<A, B>(transaction, commandTimeout: 45, commandType: CommandType.StoredProcedure);

        CommandOptions<(A, B)> converted = options;

        Assert.Same(transaction, converted.Transaction);
        Assert.Equal(45, converted.CommandTimeout);
        Assert.Equal(CommandType.StoredProcedure, converted.CommandType);
        Assert.Null(converted.ExpectedRowCount);
    }

    /// <summary>
    /// Unset stays null rather than becoming a zero capacity, which would be worse than the bug:
    /// <c>new List&lt;T&gt;(0)</c> would defeat <c>JauntyConfig.QueryResultCapacity</c> at the
    /// consuming end.
    /// </summary>
    [Fact]
    public void AnUnsetExpectedRowCountIsNullNotZero()
    {
        CommandOptions<(A, B)> converted = new MultiEntityCommandOptions<A, B>();

        Assert.Null(converted.ExpectedRowCount);
    }

    private sealed class StubTransaction : IDbTransaction
    {
        public IDbConnection? Connection => null;
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public void Commit() { }
        public void Dispose() { }
        public void Rollback() { }
    }
}
