using System.Data.Common;
using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Helpers;

using Xunit;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// AUD-R35-054. Rounds 33, 34 and 35 all filed the same open finding: nothing proves that the
/// options carried by <c>CommandOptions&lt;(T1,T2,T3)&gt;</c> or
/// <c>MultiEntityCommandOptions&lt;T1,T2,T3&gt;</c> ever reach the command. The tests that claimed to -
/// <c>Query_ThreeEntities_With[MultiEntity]CommandOptions_UsesTransaction</c> and its async twin -
/// open a transaction on a SQLite connection, write a sentinel through it and assert the sentinel
/// comes back; SQLite scopes a transaction to the connection, so those stay green with the whole
/// option-application block deleted. <c>CommandTimeout</c> and <c>CommandType</c> were never
/// asserted at arity 3 at all, on either the sync or the async side, and the async
/// <c>AsyncTransactionValidator.RequireDbTransaction</c> call was never reached by any arity-2 or
/// arity-3 test.
/// <para>
/// These assert against the command itself, via <see cref="RecordingDbConnection"/>. The findings
/// pointed at <c>QueryCore</c>/<c>QueryCoreAsync</c>; the block the arity-3 multi-entity path
/// actually runs is the one in <c>ExecuteReader.cs</c>/<c>ExecuteReaderAsync.cs</c>, which those
/// cores delegate to. Verified by perturbation against that file: suppressing the timeout
/// assignment fails 3 of these, the command-type assignment 2, dropping the transaction assignment
/// 4, and weakening <c>RequireDbTransaction</c> to an <c>as</c> cast 1.
/// </para>
/// </summary>
public class MultiEntityArity3OptionPlumbingTests
{
    internal sealed class PlumbA
    {
        public long AId { get; set; }
    }

    internal sealed class PlumbB
    {
        public long BId { get; set; }
    }

    internal sealed class PlumbC
    {
        public long CId { get; set; }
    }

    private const string Sql = "SELECT 1 AS AId, 2 AS BId, 3 AS CId";

    private static RecordingDbConnection Open()
    {
        var inner = new SQLiteConnection("Data Source=:memory:");
        inner.Open();
        return new RecordingDbConnection(inner);
    }

    [Fact]
    public void Sync_CommandOptionsTransaction_IsAssignedToTheCommand()
    {
        using var connection = Open();
        using DbTransaction transaction = connection.BeginTransaction();

        var options = new CommandOptions<(PlumbA, PlumbB, PlumbC)>(transaction: transaction);
        var rows = connection.Query<PlumbA, PlumbB, PlumbC>(Sql, options);

        Assert.Single(rows);
        Assert.Same(transaction, connection.ExecutedTransaction);
    }

    [Fact]
    public void Sync_MultiEntityCommandOptionsTransaction_IsAssignedToTheCommand()
    {
        using var connection = Open();
        using DbTransaction transaction = connection.BeginTransaction();

        var options = new MultiEntityCommandOptions<PlumbA, PlumbB, PlumbC>(transaction: transaction);
        var rows = connection.Query<PlumbA, PlumbB, PlumbC>(Sql, options);

        Assert.Single(rows);
        Assert.Same(transaction, connection.ExecutedTransaction);
    }

    [Fact]
    public void Sync_WithNoTransaction_LeavesTheCommandUnassigned()
    {
        using var connection = Open();

        var rows = connection.Query<PlumbA, PlumbB, PlumbC>(Sql, new CommandOptions<(PlumbA, PlumbB, PlumbC)>());

        Assert.Single(rows);
        Assert.Equal(1, connection.ExecutedCount);
        Assert.Null(connection.ExecutedTransaction);
    }

    [Fact]
    public void Sync_CommandTimeout_ReachesTheCommand()
    {
        using var connection = Open();

        var options = new CommandOptions<(PlumbA, PlumbB, PlumbC)>(commandTimeout: 97);
        var rows = connection.Query<PlumbA, PlumbB, PlumbC>(Sql, options);

        Assert.Single(rows);
        Assert.Equal(97, connection.ExecutedTimeout);
    }

    [Fact]
    public void Sync_MultiEntityCommandTimeout_ReachesTheCommand()
    {
        using var connection = Open();

        var options = new MultiEntityCommandOptions<PlumbA, PlumbB, PlumbC>(commandTimeout: 41);
        var rows = connection.Query<PlumbA, PlumbB, PlumbC>(Sql, options);

        Assert.Single(rows);
        Assert.Equal(41, connection.ExecutedTimeout);
    }

    /// <summary>
    /// <c>CommandType</c> is forwarded for <c>StoredProcedure</c> and <c>TableDirect</c> only, so
    /// this and the next test are two halves of one contract.
    /// </summary>
    [Fact]
    public void Sync_StoredProcedureCommandType_ReachesTheCommand()
    {
        using var connection = Open();

        var options = new CommandOptions<(PlumbA, PlumbB, PlumbC)>(commandType: CommandType.StoredProcedure);
        var rows = connection.Query<PlumbA, PlumbB, PlumbC>(Sql, options);

        Assert.Single(rows);
        Assert.Equal(CommandType.StoredProcedure, connection.ExecutedCommandType);
    }

    [Fact]
    public void Sync_TextCommandType_IsLeftAlone()
    {
        using var connection = Open();

        var options = new CommandOptions<(PlumbA, PlumbB, PlumbC)>(commandType: CommandType.Text);
        var rows = connection.Query<PlumbA, PlumbB, PlumbC>(Sql, options);

        Assert.Single(rows);
        Assert.Equal(CommandType.Text, connection.ExecutedCommandType);
    }

    [Fact]
    public async Task Async_CommandOptionsTransaction_IsAssignedToTheCommand()
    {
        using var connection = Open();
        using DbTransaction transaction = connection.BeginTransaction();

        var options = new CommandOptions<(PlumbA, PlumbB, PlumbC)>(transaction: transaction);
        var rows = await connection.QueryAsync<PlumbA, PlumbB, PlumbC>(Sql, options);

        Assert.Single(rows);
        Assert.Same(transaction, connection.ExecutedTransaction);
    }

    [Fact]
    public async Task Async_MultiEntityCommandOptionsTransaction_IsAssignedToTheCommand()
    {
        using var connection = Open();
        using DbTransaction transaction = connection.BeginTransaction();

        var options = new MultiEntityCommandOptions<PlumbA, PlumbB, PlumbC>(transaction: transaction);
        var rows = await connection.QueryAsync<PlumbA, PlumbB, PlumbC>(Sql, options);

        Assert.Single(rows);
        Assert.Same(transaction, connection.ExecutedTransaction);
    }

    /// <summary>
    /// The assertion only the plumbing can produce: <c>RequireDbTransaction</c> rejects an
    /// <see cref="IDbTransaction"/> that is not a <see cref="DbTransaction"/>. Nothing at arity 2 or
    /// 3 reached this call before.
    /// </summary>
    [Fact]
    public async Task Async_NonDbTransaction_IsRejectedByTheValidator()
    {
        using var connection = Open();
        using DbTransaction real = connection.BeginTransaction();
        using var wrapped = new IDbTransactionWrapper(real);

        var options = new CommandOptions<(PlumbA, PlumbB, PlumbC)>(transaction: wrapped);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            async () => await connection.QueryAsync<PlumbA, PlumbB, PlumbC>(Sql, options));

        Assert.Contains("DbTransaction", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Async_CommandTimeout_ReachesTheCommand()
    {
        using var connection = Open();

        var options = new CommandOptions<(PlumbA, PlumbB, PlumbC)>(commandTimeout: 73);
        var rows = await connection.QueryAsync<PlumbA, PlumbB, PlumbC>(Sql, options);

        Assert.Single(rows);
        Assert.Equal(73, connection.ExecutedTimeout);
    }

    [Fact]
    public async Task Async_StoredProcedureCommandType_ReachesTheCommand()
    {
        using var connection = Open();

        var options = new MultiEntityCommandOptions<PlumbA, PlumbB, PlumbC>(commandType: CommandType.StoredProcedure);
        var rows = await connection.QueryAsync<PlumbA, PlumbB, PlumbC>(Sql, options);

        Assert.Single(rows);
        Assert.Equal(CommandType.StoredProcedure, connection.ExecutedCommandType);
    }

    /// <summary>
    /// AUD-R35-055. AUD-R34-001 made every multi-entity core honour <c>options.Mapper</c> by
    /// delegating to the single-entity core, but the only tests supplying a mapper to a
    /// multi-entity call are arity-2, so the arity-3 delegation would pass the suite if reverted.
    /// The regression mode is silent: reflection mapping returns plausible rows.
    /// </summary>
    [Fact]
    public void Sync_Mapper_IsHonouredAtArityThree()
    {
        using var connection = Open();

        int calls = 0;
        var options = CommandOptions<(PlumbA, PlumbB, PlumbC)>.WithMapper(reader =>
        {
            calls++;
            return (new PlumbA { AId = 100 }, new PlumbB { BId = 200 }, new PlumbC { CId = 300 });
        });

        var rows = connection.Query<PlumbA, PlumbB, PlumbC>(Sql, options);

        Assert.Equal(1, calls);
        Assert.Equal(100L, rows[0].Item1.AId);
        Assert.Equal(200L, rows[0].Item2.BId);
        Assert.Equal(300L, rows[0].Item3.CId);
    }

    [Fact]
    public async Task Async_Mapper_IsHonouredAtArityThree()
    {
        using var connection = Open();

        int calls = 0;
        var options = CommandOptions<(PlumbA, PlumbB, PlumbC)>.WithMapper(reader =>
        {
            calls++;
            return (new PlumbA { AId = 100 }, new PlumbB { BId = 200 }, new PlumbC { CId = 300 });
        });

        var rows = await connection.QueryAsync<PlumbA, PlumbB, PlumbC>(Sql, options);

        Assert.Equal(1, calls);
        Assert.Equal(100L, rows[0].Item1.AId);
        Assert.Equal(200L, rows[0].Item2.BId);
        Assert.Equal(300L, rows[0].Item3.CId);
    }

    /// <summary>
    /// The mapper wins over the built-in multi-entity mapping rather than merely running alongside
    /// it - the values above are not in the result set at all.
    /// </summary>
    [Fact]
    public void Sync_Mapper_ReplacesTheBuiltInMappingRatherThanSupplementingIt()
    {
        using var connection = Open();

        var withoutMapper = connection.Query<PlumbA, PlumbB, PlumbC>(Sql, new CommandOptions<(PlumbA, PlumbB, PlumbC)>());

        Assert.Equal(1L, withoutMapper[0].Item1.AId);
        Assert.Equal(2L, withoutMapper[0].Item2.BId);
        Assert.Equal(3L, withoutMapper[0].Item3.CId);
    }
}
