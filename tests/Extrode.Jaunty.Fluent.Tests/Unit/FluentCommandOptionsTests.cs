using System.Data;
using System.Data.Common;

using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Core;
using Extrode.Jaunty.Fluent;

using Xunit;

namespace Extrode.Jaunty.Fluent.Tests.Unit;

/// <summary>
/// AUD-R26-060 (batch 5, low/consistency). <see cref="CommandOptions"/> is the only way to enlist a
/// fluent call in a caller's transaction or set a command timeout, and it reached the terminals that
/// delegate to core while never reaching the ones that build and execute their own command.
///
/// <para>
/// The sharpest form: <c>InsertBuilder</c> exposed <c>Insert()</c> and
/// <c>InsertAsync(CancellationToken)</c> and nothing else, and <c>ExecuteInsert</c> took no options
/// parameter - so there was no way to pass a transaction to a fluent insert at all.
/// <c>GroupedQueryBuilder.Select</c>/<c>SelectAsync</c> had the same hole. Meanwhile
/// <c>QueryBuilder.Select</c>/<c>SelectFirst</c>/<c>Count</c>/<c>Delete</c>/<c>Update</c> all carry
/// an options overload.
/// </para>
///
/// <para>
/// The gap was provider-dependent rather than uniformly broken, which is what kept it quiet: on
/// Microsoft.Data.Sqlite a fluent insert inside an ambient <c>BeginTransaction</c> is rolled back
/// with it anyway, because SQLite associates commands with the connection's open transaction
/// implicitly. <c>SqlClient</c> throws when a command with no <c>Transaction</c> runs on a
/// connection with a pending local transaction, so the same application code behaves differently per
/// provider. That is why these tests assert on what is assigned to the command rather than on
/// whether a rollback took effect - a SQLite round-trip cannot tell the two behaviours apart.
/// </para>
///
/// <para>
/// The joined builders' self-executing terminals (<c>SelectBoth</c>, <c>Select&lt;T&gt;</c>,
/// <c>Select&lt;T&gt;(mapper)</c> and friends) still have no options overload; that half is carried
/// forward deliberately and the reason is recorded on <c>SelectBothInternal</c>. The grouped
/// joined builders are no longer part of that carry-forward: CF-9 / AUD-R31-007 gave
/// <c>GroupedJoinedQueryBuilder{,3,4}</c> the same overloads, covered at the bottom of this file.
/// </para>
/// </summary>
public class FluentCommandOptionsTests
{
    [Table("opt_products")]
    public class Product
    {
        [Key]
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public int SupplierId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Table("opt_categories")]
    public class Category
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Table("opt_suppliers")]
    public class Supplier
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Table("opt_orders")]
    public class Order
    {
        [Key]
        public int Id { get; set; }
        public int EmployeeId { get; set; }
    }

    // ------------------------------------------------------------------
    // Insert - the terminal that had no options overload at all
    // ------------------------------------------------------------------

    [Fact]
    public void Insert_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new SqliteConnection();
        var transaction = new StubTransaction();

        connection.Into<Product>()
            .Value(p => p.Name, "widget")
            .Insert(new CommandOptions(transaction: transaction));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public void Insert_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new SqliteConnection();

        connection.Into<Product>()
            .Value(p => p.Name, "widget")
            .Insert(new CommandOptions(commandTimeout: 77));

        Assert.Equal(77, connection.LastCommand!.CommandTimeout);
    }

    /// <summary>
    /// The no-argument overload must keep behaving exactly as it did - it now forwards
    /// <c>default</c>, and a default <see cref="CommandOptions"/> must leave the command untouched
    /// rather than, say, stamping a zero timeout (which means "no timeout" to some providers and is
    /// a very different thing from "provider default").
    /// </summary>
    [Fact]
    public void Insert_WithoutOptions_LeavesTheCommandUntouched()
    {
        var connection = new SqliteConnection();

        connection.Into<Product>()
            .Value(p => p.Name, "widget")
            .Insert();

        Assert.Null(connection.LastCommand!.Transaction);
        Assert.Equal(0, connection.LastCommand.CommandTimeout);
        Assert.Equal(CommandType.Text, connection.LastCommand.CommandType);
    }

    /// <summary>
    /// <see cref="CommandOptions"/> is a struct whose primary constructor does not supply the
    /// parameterless one, so <c>default</c> carries <c>CommandType 0</c> - which is not a defined
    /// <see cref="CommandType"/>; <see cref="CommandType.Text"/> is 1. A "set it unless it is Text"
    /// guard therefore stamps that undefined 0 onto every command built from default options, and
    /// the first draft of <c>FluentCommandOptions</c> did exactly that. Core avoids it by setting
    /// the command type only for the two values that need it (<c>ExecuteNonQueryCore</c>), which is
    /// the convention this now follows.
    /// </summary>
    [Theory]
    [InlineData(CommandType.StoredProcedure)]
    [InlineData(CommandType.TableDirect)]
    public void Insert_WithANonTextCommandType_PassesItThrough(CommandType commandType)
    {
        var connection = new SqliteConnection();

        connection.Into<Product>()
            .Value(p => p.Name, "widget")
            .Insert(new CommandOptions(commandType: commandType));

        Assert.Equal(commandType, connection.LastCommand!.CommandType);
    }

    [Fact]
    public void Insert_WithAnExplicitTextCommandType_LeavesTheCommandAlone()
    {
        var connection = new SqliteConnection();

        connection.Into<Product>()
            .Value(p => p.Name, "widget")
            .Insert(new CommandOptions(commandType: CommandType.Text));

        // Not asserting "== Text" - the point is that nothing was assigned. SQLite's real provider
        // throws on command types it does not support, so a redundant write is not free.
        Assert.Equal(CommandType.Text, connection.LastCommand!.CommandType);
        Assert.False(connection.LastCommand.CommandTypeWasSet);
    }

    [Fact]
    public async Task InsertAsync_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new Async.SqliteConnection();
        DbTransaction transaction = connection.BeginTransaction();

        await connection.Into<Product>()
            .Value(p => p.Name, "widget")
            .InsertAsync(new CommandOptions(transaction: transaction));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public async Task InsertAsync_WithoutOptions_LeavesTheCommandUntouched()
    {
        var connection = new Async.SqliteConnection();

        await connection.Into<Product>()
            .Value(p => p.Name, "widget")
            .InsertAsync();

        Assert.Null(connection.LastCommand!.Transaction);
        Assert.Equal(0, connection.LastCommand.CommandTimeout);
    }

    /// <summary>
    /// A <see cref="DbConnection"/>'s <c>IDbCommand.Transaction</c> setter casts to
    /// <see cref="DbTransaction"/> internally, so handing it a plain <see cref="IDbTransaction"/>
    /// would throw an opaque <see cref="InvalidCastException"/> from inside ADO.NET. Validating
    /// first is what <c>QueryBuilder.ExecuteNonQuery</c> already did; <c>FluentCommandOptions</c>
    /// exists so the three builders share that rather than each rediscovering it.
    /// </summary>
    [Fact]
    public async Task InsertAsync_WithANonDbTransaction_ThrowsJauntysOwnError()
    {
        var connection = new Async.SqliteConnection();

        ArgumentException ex = await Assert.ThrowsAnyAsync<ArgumentException>(
            () => connection.Into<Product>()
                .Value(p => p.Name, "widget")
                .InsertAsync(new CommandOptions(transaction: new StubTransaction())));

        Assert.IsNotType<InvalidCastException>(ex);
    }

    // ------------------------------------------------------------------
    // Grouped select - the other terminal that executes its own command
    // ------------------------------------------------------------------

    [Fact]
    public void GroupedSelect_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new SqliteConnection();
        var transaction = new StubTransaction();

        connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() }, new CommandOptions(transaction: transaction));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public void GroupedSelect_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new SqliteConnection();

        connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() }, new CommandOptions(commandTimeout: 41));

        Assert.Equal(41, connection.LastCommand!.CommandTimeout);
    }

    [Fact]
    public void GroupedSelect_WithoutOptions_LeavesTheCommandUntouched()
    {
        var connection = new SqliteConnection();

        connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.Null(connection.LastCommand!.Transaction);
        Assert.Equal(0, connection.LastCommand.CommandTimeout);
    }

    [Fact]
    public async Task GroupedSelectAsync_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new Async.SqliteConnection();
        DbTransaction transaction = connection.BeginTransaction();

        await connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() }, new CommandOptions(transaction: transaction));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public async Task GroupedSelectAsync_WithoutOptions_StillCompiles_AndLeavesTheCommandUntouched()
    {
        // The cancellation-token-only overload must remain callable without ambiguity now that an
        // options overload with a defaulted token sits beside it.
        var connection = new Async.SqliteConnection();

        await connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() }, CancellationToken.None);

        Assert.Null(connection.LastCommand!.Transaction);
    }

    // ------------------------------------------------------------------
    // Grouped joined select - CF-9 / AUD-R31-007. Asserted here rather than against a real SQLite
    // file for the reason in this class's doc comment: SQLite associates commands with the
    // connection's open transaction implicitly, so a round-trip passes whether or not the option
    // ever reaches the command.
    // ------------------------------------------------------------------

    [Fact]
    public void GroupedJoinedSelect_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new SqliteConnection();
        var transaction = new StubTransaction();

        connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .GroupBy((p, c) => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() }, new CommandOptions(transaction: transaction));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public void GroupedJoinedSelect_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new SqliteConnection();

        connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .GroupBy((p, c) => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() }, new CommandOptions(commandTimeout: 42));

        Assert.Equal(42, connection.LastCommand!.CommandTimeout);
    }

    [Fact]
    public void GroupedJoinedSelect_WithoutOptions_LeavesTheCommandUntouched()
    {
        var connection = new SqliteConnection();

        connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .GroupBy((p, c) => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.Null(connection.LastCommand!.Transaction);
        Assert.Equal(0, connection.LastCommand.CommandTimeout);
    }

    [Fact]
    public async Task GroupedJoinedSelectAsync_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new Async.SqliteConnection();
        DbTransaction transaction = connection.BeginTransaction();

        await connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .GroupBy((p, c) => p.CategoryId)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() }, new CommandOptions(transaction: transaction));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public async Task GroupedJoinedSelectAsync_WithCancellationTokenOnly_StaysUnambiguous()
    {
        var connection = new Async.SqliteConnection();

        await connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .GroupBy((p, c) => p.CategoryId)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() }, CancellationToken.None);

        Assert.Null(connection.LastCommand!.Transaction);
    }

    [Fact]
    public void GroupedJoined3Select_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new SqliteConnection();
        var transaction = new StubTransaction();

        connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.Id)
            .GroupBy((p, c, s) => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() }, new CommandOptions(transaction: transaction));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public void GroupedJoined3Select_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new SqliteConnection();

        connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.Id)
            .GroupBy((p, c, s) => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() }, new CommandOptions(commandTimeout: 43));

        Assert.Equal(43, connection.LastCommand!.CommandTimeout);
    }

    [Fact]
    public async Task GroupedJoined3SelectAsync_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new Async.SqliteConnection();
        DbTransaction transaction = connection.BeginTransaction();

        await connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.Id)
            .GroupBy((p, c, s) => p.CategoryId)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() }, new CommandOptions(transaction: transaction));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public void GroupedJoined4Select_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new SqliteConnection();
        var transaction = new StubTransaction();

        connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.Id)
            .InnerJoin<Product, Category, Supplier, Order>().On("opt_products.id", "opt_orders.employee_id")
            .GroupBy((p, c, s, o) => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() }, new CommandOptions(transaction: transaction));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public void GroupedJoined4Select_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new SqliteConnection();

        connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.Id)
            .InnerJoin<Product, Category, Supplier, Order>().On("opt_products.id", "opt_orders.employee_id")
            .GroupBy((p, c, s, o) => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() }, new CommandOptions(commandTimeout: 44));

        Assert.Equal(44, connection.LastCommand!.CommandTimeout);
    }

    [Fact]
    public async Task GroupedJoined4SelectAsync_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new Async.SqliteConnection();
        DbTransaction transaction = connection.BeginTransaction();

        await connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.Id)
            .InnerJoin<Product, Category, Supplier, Order>().On("opt_products.id", "opt_orders.employee_id")
            .GroupBy((p, c, s, o) => p.CategoryId)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() }, new CommandOptions(transaction: transaction));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    // ------------------------------------------------------------------
    // SetOperationBuilder (UNION/UNION ALL/EXCEPT/INTERSECT) terminals - AUD-R32. None of the
    // CommandOptions-accepting overloads on ISetOperationClause<T>/ISetOperationOrderByClause<T>
    // had a test, including the private ToTypedOptions<TResult> helper every one of them funnels
    // through. Same reasoning as the rest of this file: SQLite binds a command to the connection's
    // open transaction implicitly, so only a capturing IDbCommand can prove the option reached it.
    // ------------------------------------------------------------------

    private static ISetOperationClause<Product> BuildUnion(IDbConnection connection) =>
        connection.From<Product>()
            .Where(p => p.Name == "a")
            .Union(connection.From<Product>().Where(p => p.Name == "b"));

    [Fact]
    public void SetOperationSelect_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new SqliteConnection();
        var transaction = new StubTransaction();

        BuildUnion(connection).Select(new CommandOptions(transaction: transaction));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public void SetOperationSelect_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new SqliteConnection();

        BuildUnion(connection).Select(new CommandOptions(commandTimeout: 55));

        Assert.Equal(55, connection.LastCommand!.CommandTimeout);
    }

    [Fact]
    public void SetOperationSelect_WithoutOptions_LeavesTheCommandUntouched()
    {
        var connection = new SqliteConnection();

        BuildUnion(connection).Select();

        Assert.Null(connection.LastCommand!.Transaction);
        Assert.Equal(0, connection.LastCommand.CommandTimeout);
    }

    [Fact]
    public void SetOperationSelectFirst_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new SqliteConnection();
        var transaction = new StubTransaction();

        // EmptyReader.Read() is always false, so SelectFirst throws for lack of rows once it
        // reaches ExecuteReader - but options are assigned to the command before that point, so
        // the assertion below is unaffected by the (expected) exception.
        Assert.ThrowsAny<InvalidOperationException>(
            () => BuildUnion(connection).SelectFirst(new CommandOptions(transaction: transaction)));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public void SetOperationSelectFirst_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new SqliteConnection();

        Assert.ThrowsAny<InvalidOperationException>(
            () => BuildUnion(connection).SelectFirst(new CommandOptions(commandTimeout: 56)));

        Assert.Equal(56, connection.LastCommand!.CommandTimeout);
    }

    [Fact]
    public void SetOperationSelectFirstOrDefault_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new SqliteConnection();
        var transaction = new StubTransaction();

        Product? result = BuildUnion(connection).SelectFirstOrDefault(new CommandOptions(transaction: transaction));

        Assert.Null(result);
        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public void SetOperationSelectFirstOrDefault_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new SqliteConnection();

        Product? result = BuildUnion(connection).SelectFirstOrDefault(new CommandOptions(commandTimeout: 57));

        Assert.Null(result);
        Assert.Equal(57, connection.LastCommand!.CommandTimeout);
    }

    [Fact]
    public void SetOperationSelectSingle_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new SqliteConnection();
        var transaction = new StubTransaction();

        Assert.ThrowsAny<InvalidOperationException>(
            () => BuildUnion(connection).SelectSingle(new CommandOptions(transaction: transaction)));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public void SetOperationSelectSingle_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new SqliteConnection();

        Assert.ThrowsAny<InvalidOperationException>(
            () => BuildUnion(connection).SelectSingle(new CommandOptions(commandTimeout: 58)));

        Assert.Equal(58, connection.LastCommand!.CommandTimeout);
    }

    [Fact]
    public void SetOperationSelectSingleOrDefault_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new SqliteConnection();
        var transaction = new StubTransaction();

        Product? result = BuildUnion(connection).SelectSingleOrDefault(new CommandOptions(transaction: transaction));

        Assert.Null(result);
        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public void SetOperationSelectSingleOrDefault_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new SqliteConnection();

        Product? result = BuildUnion(connection).SelectSingleOrDefault(new CommandOptions(commandTimeout: 59));

        Assert.Null(result);
        Assert.Equal(59, connection.LastCommand!.CommandTimeout);
    }

    [Fact]
    public async Task SetOperationSelectAsync_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new Async.SqliteConnection();
        DbTransaction transaction = connection.BeginTransaction();

        await BuildUnion(connection).SelectAsync(new CommandOptions(transaction: transaction));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public async Task SetOperationSelectAsync_WithoutOptions_LeavesTheCommandUntouched()
    {
        var connection = new Async.SqliteConnection();

        await BuildUnion(connection).SelectAsync(CancellationToken.None);

        Assert.Null(connection.LastCommand!.Transaction);
    }

    [Fact]
    public async Task SetOperationSelectFirstAsync_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new Async.SqliteConnection();
        DbTransaction transaction = connection.BeginTransaction();

        await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => BuildUnion(connection).SelectFirstAsync(new CommandOptions(transaction: transaction)));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public async Task SetOperationSelectFirstAsync_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new Async.SqliteConnection();

        await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => BuildUnion(connection).SelectFirstAsync(new CommandOptions(commandTimeout: 60)));

        Assert.Equal(60, connection.LastCommand!.CommandTimeout);
    }

    [Fact]
    public async Task SetOperationSelectFirstOrDefaultAsync_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new Async.SqliteConnection();
        DbTransaction transaction = connection.BeginTransaction();

        Product? result = await BuildUnion(connection).SelectFirstOrDefaultAsync(new CommandOptions(transaction: transaction));

        Assert.Null(result);
        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public async Task SetOperationSelectFirstOrDefaultAsync_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new Async.SqliteConnection();

        Product? result = await BuildUnion(connection).SelectFirstOrDefaultAsync(new CommandOptions(commandTimeout: 61));

        Assert.Null(result);
        Assert.Equal(61, connection.LastCommand!.CommandTimeout);
    }

    [Fact]
    public async Task SetOperationSelectSingleAsync_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new Async.SqliteConnection();
        DbTransaction transaction = connection.BeginTransaction();

        await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => BuildUnion(connection).SelectSingleAsync(new CommandOptions(transaction: transaction)));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public async Task SetOperationSelectSingleAsync_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new Async.SqliteConnection();

        await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => BuildUnion(connection).SelectSingleAsync(new CommandOptions(commandTimeout: 62)));

        Assert.Equal(62, connection.LastCommand!.CommandTimeout);
    }

    [Fact]
    public async Task SetOperationSelectSingleOrDefaultAsync_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new Async.SqliteConnection();
        DbTransaction transaction = connection.BeginTransaction();

        Product? result = await BuildUnion(connection).SelectSingleOrDefaultAsync(new CommandOptions(transaction: transaction));

        Assert.Null(result);
        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public async Task SetOperationSelectSingleOrDefaultAsync_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new Async.SqliteConnection();

        Product? result = await BuildUnion(connection).SelectSingleOrDefaultAsync(new CommandOptions(commandTimeout: 63));

        Assert.Null(result);
        Assert.Equal(63, connection.LastCommand!.CommandTimeout);
    }

    // ------------------------------------------------------------------
    // AUD-R34-016: SelectAll on the 3- and 4-way joined builders. These build and execute their
    // own command and had no options overload at all, so the tuple-returning surface of a wide
    // join could not join the caller's transaction - a throw on SqlClient, not a lost option.
    // ------------------------------------------------------------------

    [Fact]
    public void JoinedSelectAll3_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new SqliteConnection();
        var transaction = new StubTransaction();

        connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.Id)
            .SelectAll(new CommandOptions(transaction: transaction));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public void JoinedSelectAll3_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new SqliteConnection();

        connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.Id)
            .SelectAll(new CommandOptions(commandTimeout: 71));

        Assert.Equal(71, connection.LastCommand!.CommandTimeout);
    }

    [Fact]
    public void JoinedSelectAll3_WithoutOptions_LeavesTheCommandUntouched()
    {
        var connection = new SqliteConnection();

        connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.Id)
            .SelectAll();

        Assert.Null(connection.LastCommand!.Transaction);
    }

    [Fact]
    public async Task JoinedSelectAllAsync3_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new Async.SqliteConnection();
        DbTransaction transaction = connection.BeginTransaction();

        await connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.Id)
            .SelectAllAsync(new CommandOptions(transaction: transaction));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public async Task JoinedSelectAllAsync3_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new Async.SqliteConnection();

        await connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.Id)
            .SelectAllAsync(new CommandOptions(commandTimeout: 72));

        Assert.Equal(72, connection.LastCommand!.CommandTimeout);
    }

    [Fact]
    public void JoinedSelectAll4_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new SqliteConnection();
        var transaction = new StubTransaction();

        connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.Id)
            .LeftJoin<Product, Category, Supplier, Order>().On("1 = 1")
            .SelectAll(new CommandOptions(transaction: transaction));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public void JoinedSelectAll4_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new SqliteConnection();

        connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.Id)
            .LeftJoin<Product, Category, Supplier, Order>().On("1 = 1")
            .SelectAll(new CommandOptions(commandTimeout: 73));

        Assert.Equal(73, connection.LastCommand!.CommandTimeout);
    }

    [Fact]
    public void JoinedSelectAll4_WithoutOptions_LeavesTheCommandUntouched()
    {
        var connection = new SqliteConnection();

        connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.Id)
            .LeftJoin<Product, Category, Supplier, Order>().On("1 = 1")
            .SelectAll();

        Assert.Null(connection.LastCommand!.Transaction);
    }

    [Fact]
    public async Task JoinedSelectAllAsync4_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new Async.SqliteConnection();
        DbTransaction transaction = connection.BeginTransaction();

        await connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.Id)
            .LeftJoin<Product, Category, Supplier, Order>().On("1 = 1")
            .SelectAllAsync(new CommandOptions(transaction: transaction));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public async Task JoinedSelectAllAsync4_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new Async.SqliteConnection();

        await connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.Id)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.Id)
            .LeftJoin<Product, Category, Supplier, Order>().On("1 = 1")
            .SelectAllAsync(new CommandOptions(commandTimeout: 74));

        Assert.Equal(74, connection.LastCommand!.CommandTimeout);
    }

    // ------------------------------------------------------------------
    // AUD-R34-017: the CTE builder's four terminals executed through the IDbConnection extension
    // methods with no options argument, so a CTE query could not be enlisted in the caller's
    // transaction at all.
    // ------------------------------------------------------------------

    [Fact]
    public void CteSelect_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new SqliteConnection();
        var transaction = new StubTransaction();

        connection.Cte<Product>("Cheap")
            .As(q => q.Where(p => p.Id > 0))
            .Select(new CommandOptions(transaction: transaction));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public void CteSelect_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new SqliteConnection();

        connection.Cte<Product>("Cheap")
            .As(q => q.Where(p => p.Id > 0))
            .Select(new CommandOptions(commandTimeout: 81));

        Assert.Equal(81, connection.LastCommand!.CommandTimeout);
    }

    [Fact]
    public void CteSelect_WithoutOptions_LeavesTheCommandUntouched()
    {
        var connection = new SqliteConnection();

        connection.Cte<Product>("Cheap")
            .As(q => q.Where(p => p.Id > 0))
            .Select();

        Assert.Null(connection.LastCommand!.Transaction);
    }

    [Fact]
    public async Task CteSelectAsync_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new Async.SqliteConnection();
        DbTransaction transaction = connection.BeginTransaction();

        await connection.Cte<Product>("Cheap")
            .As(q => q.Where(p => p.Id > 0))
            .SelectAsync(new CommandOptions(transaction: transaction));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public async Task CteSelectAsync_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new Async.SqliteConnection();

        await connection.Cte<Product>("Cheap")
            .As(q => q.Where(p => p.Id > 0))
            .SelectAsync(new CommandOptions(commandTimeout: 82));

        Assert.Equal(82, connection.LastCommand!.CommandTimeout);
    }

    [Fact]
    public void CteSelectFirst_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new SqliteConnection();
        var transaction = new StubTransaction();

        try
        {
            connection.Cte<Product>("Cheap")
                .As(q => q.Where(p => p.Id > 0))
                .SelectFirst(new CommandOptions(transaction: transaction));
        }
        catch (InvalidOperationException)
        {
            // The stub reader returns no rows; QueryFirst throws after the command was built.
        }

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public void CteSelectFirst_WithOptions_StillLimitsToOneRow()
    {
        var connection = new SqliteConnection();

        try
        {
            connection.Cte<Product>("Cheap")
                .As(q => q.Where(p => p.Id > 0))
                .SelectFirst(new CommandOptions(commandTimeout: 83));
        }
        catch (InvalidOperationException)
        {
        }

        Assert.Equal(83, connection.LastCommand!.CommandTimeout);
        Assert.Contains("1", connection.LastCommand!.CommandText, StringComparison.Ordinal);
    }

    [Fact]
    public void CteSelectFirstOrDefault_WithOptions_EnlistsTheCallersTransaction()
    {
        var connection = new SqliteConnection();
        var transaction = new StubTransaction();

        connection.Cte<Product>("Cheap")
            .As(q => q.Where(p => p.Id > 0))
            .SelectFirstOrDefault(new CommandOptions(transaction: transaction));

        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }

    [Fact]
    public void CteSelectFirstOrDefault_WithOptions_AppliesTheCommandTimeout()
    {
        var connection = new SqliteConnection();

        connection.Cte<Product>("Cheap")
            .As(q => q.Where(p => p.Id > 0))
            .SelectFirstOrDefault(new CommandOptions(commandTimeout: 84));

        Assert.Equal(84, connection.LastCommand!.CommandTimeout);
    }

    [Fact]
    public void CteSelectFirstOrDefault_WithOptions_RestoresTheTakeCount()
    {
        var connection = new SqliteConnection();

        ICteQueryClause<Product> cte = connection.Cte<Product>("Cheap")
            .As(q => q.Where(p => p.Id > 0));

        cte.SelectFirstOrDefault(new CommandOptions(commandTimeout: 85));
        var firstSql = connection.LastCommand!.CommandText;

        cte.Select(new CommandOptions(commandTimeout: 86));
        var allSql = connection.LastCommand!.CommandText;

        Assert.NotEqual(firstSql, allSql);
    }

    // ------------------------------------------------------------------
    // AUD-R38-031: joined SelectPartial terminals build their own command and had no options
    // overload, so none could join the caller's transaction.
    // ------------------------------------------------------------------

    private static IJoinedQuery<Product, Category> Join2(IDbConnection connection)
        => connection.From<Product>().InnerJoin<Category>().On(p => p.CategoryId, c => c.Id);

    private static IJoinedQuery3<Product, Category, Supplier> Join3(IDbConnection connection)
        => Join2(connection).InnerJoin<Supplier>().On(p => p.SupplierId, s => s.Id);

    private static IJoinedQuery4<Product, Category, Supplier, Order> Join4(IDbConnection connection)
        => Join3(connection).InnerJoin<Product, Category, Supplier, Order>().On("opt_products.id", "opt_orders.employee_id");

    private static readonly Dictionary<string, Action<IDbConnection, CommandOptions>> SelectPartialTerminals = new()
    {
        ["2:SelectPartial"] = (c, o) => Join2(c).SelectPartial("opt_products.name", o),
        ["2:SelectPartial<T>"] = (c, o) => Join2(c).SelectPartial("opt_products.name", r => 1, o),
        ["2:SelectPartialFirst"] = (c, o) => Join2(c).SelectPartialFirst("opt_products.name", o),
        ["2:SelectPartialFirst<T>"] = (c, o) => Join2(c).SelectPartialFirst("opt_products.name", r => 1, o),
        ["2:SelectPartialFirstOrDefault"] = (c, o) => Join2(c).SelectPartialFirstOrDefault("opt_products.name", o),
        ["2:SelectPartialFirstOrDefault<T>"] = (c, o) => Join2(c).SelectPartialFirstOrDefault("opt_products.name", r => 1, o),
        ["2:SelectPartialSingle"] = (c, o) => Join2(c).SelectPartialSingle("opt_products.name", o),
        ["2:SelectPartialSingle<T>"] = (c, o) => Join2(c).SelectPartialSingle("opt_products.name", r => 1, o),
        ["2:SelectPartialSingleOrDefault"] = (c, o) => Join2(c).SelectPartialSingleOrDefault("opt_products.name", o),
        ["2:SelectPartialSingleOrDefault<T>"] = (c, o) => Join2(c).SelectPartialSingleOrDefault("opt_products.name", r => 1, o),
        ["3:SelectPartial"] = (c, o) => Join3(c).SelectPartial("opt_products.name", o),
        ["3:SelectPartialFirst"] = (c, o) => Join3(c).SelectPartialFirst("opt_products.name", o),
        ["3:SelectPartialFirstOrDefault"] = (c, o) => Join3(c).SelectPartialFirstOrDefault("opt_products.name", o),
        ["3:SelectPartialSingle"] = (c, o) => Join3(c).SelectPartialSingle("opt_products.name", o),
        ["3:SelectPartialSingleOrDefault"] = (c, o) => Join3(c).SelectPartialSingleOrDefault("opt_products.name", o),
        ["4:SelectPartial"] = (c, o) => Join4(c).SelectPartial("opt_products.name", o),
        ["4:SelectPartialFirst"] = (c, o) => Join4(c).SelectPartialFirst("opt_products.name", o),
        ["4:SelectPartialFirstOrDefault"] = (c, o) => Join4(c).SelectPartialFirstOrDefault("opt_products.name", o),
        ["4:SelectPartialSingle"] = (c, o) => Join4(c).SelectPartialSingle("opt_products.name", o),
        ["4:SelectPartialSingleOrDefault"] = (c, o) => Join4(c).SelectPartialSingleOrDefault("opt_products.name", o),
    };

    private static readonly Dictionary<string, Func<DbConnection, CommandOptions, Task>> SelectPartialAsyncTerminals = new()
    {
        ["2:SelectPartialAsync"] = (c, o) => Join2(c).SelectPartialAsync("opt_products.name", o),
        ["2:SelectPartialAsync<T>"] = (c, o) => Join2(c).SelectPartialAsync("opt_products.name", r => 1, o),
        ["2:SelectPartialFirstAsync"] = (c, o) => Join2(c).SelectPartialFirstAsync("opt_products.name", o),
        ["2:SelectPartialFirstAsync<T>"] = (c, o) => Join2(c).SelectPartialFirstAsync("opt_products.name", r => 1, o),
        ["2:SelectPartialFirstOrDefaultAsync"] = (c, o) => Join2(c).SelectPartialFirstOrDefaultAsync("opt_products.name", o),
        ["2:SelectPartialFirstOrDefaultAsync<T>"] = (c, o) => Join2(c).SelectPartialFirstOrDefaultAsync("opt_products.name", r => 1, o),
        ["2:SelectPartialSingleAsync"] = (c, o) => Join2(c).SelectPartialSingleAsync("opt_products.name", o),
        ["2:SelectPartialSingleAsync<T>"] = (c, o) => Join2(c).SelectPartialSingleAsync("opt_products.name", r => 1, o),
        ["2:SelectPartialSingleOrDefaultAsync"] = (c, o) => Join2(c).SelectPartialSingleOrDefaultAsync("opt_products.name", o),
        ["2:SelectPartialSingleOrDefaultAsync<T>"] = (c, o) => Join2(c).SelectPartialSingleOrDefaultAsync("opt_products.name", r => 1, o),
        ["3:SelectPartialAsync"] = (c, o) => Join3(c).SelectPartialAsync("opt_products.name", o),
        ["3:SelectPartialFirstAsync"] = (c, o) => Join3(c).SelectPartialFirstAsync("opt_products.name", o),
        ["3:SelectPartialFirstOrDefaultAsync"] = (c, o) => Join3(c).SelectPartialFirstOrDefaultAsync("opt_products.name", o),
        ["3:SelectPartialSingleAsync"] = (c, o) => Join3(c).SelectPartialSingleAsync("opt_products.name", o),
        ["3:SelectPartialSingleOrDefaultAsync"] = (c, o) => Join3(c).SelectPartialSingleOrDefaultAsync("opt_products.name", o),
        ["4:SelectPartialAsync"] = (c, o) => Join4(c).SelectPartialAsync("opt_products.name", o),
        ["4:SelectPartialFirstAsync"] = (c, o) => Join4(c).SelectPartialFirstAsync("opt_products.name", o),
        ["4:SelectPartialFirstOrDefaultAsync"] = (c, o) => Join4(c).SelectPartialFirstOrDefaultAsync("opt_products.name", o),
        ["4:SelectPartialSingleAsync"] = (c, o) => Join4(c).SelectPartialSingleAsync("opt_products.name", o),
        ["4:SelectPartialSingleOrDefaultAsync"] = (c, o) => Join4(c).SelectPartialSingleOrDefaultAsync("opt_products.name", o),
    };

    public static TheoryData<string> SelectPartialTerminalNames() => new(SelectPartialTerminals.Keys);

    public static TheoryData<string> SelectPartialAsyncTerminalNames() => new(SelectPartialAsyncTerminals.Keys);

    [Theory]
    [MemberData(nameof(SelectPartialTerminalNames))]
    public void JoinedSelectPartial_WithOptions_AppliesTheTransactionAndTimeout(string terminal)
    {
        var connection = new SqliteConnection();
        var transaction = new StubTransaction();

        try
        {
            SelectPartialTerminals[terminal](connection, new CommandOptions(transaction: transaction, commandTimeout: 37));
        }
        catch (InvalidOperationException)
        {
        }

        Assert.Same(transaction, connection.LastCommand!.Transaction);
        Assert.Equal(37, connection.LastCommand.CommandTimeout);
    }

    [Theory]
    [MemberData(nameof(SelectPartialAsyncTerminalNames))]
    public async Task JoinedSelectPartialAsync_WithOptions_AppliesTheTransactionAndTimeout(string terminal)
    {
        var connection = new Async.SqliteConnection();
        DbTransaction transaction = connection.BeginTransaction();

        try
        {
            await SelectPartialAsyncTerminals[terminal](connection, new CommandOptions(transaction: transaction, commandTimeout: 38));
        }
        catch (InvalidOperationException)
        {
        }

        Assert.Same(transaction, connection.LastCommand!.Transaction);
        Assert.Equal(38, connection.LastCommand.CommandTimeout);
    }

    [Fact]
    public void JoinedSelectPartial_WithoutOptions_LeavesTheCommandUntouched()
    {
        var connection = new SqliteConnection();

        Join2(connection).SelectPartial("opt_products.name");

        Assert.Null(connection.LastCommand!.Transaction);
        Assert.Equal(0, connection.LastCommand.CommandTimeout);
    }

    [Fact]
    public async Task JoinedSelectPartialAsync_WithANonDbTransaction_ThrowsJauntysOwnError()
    {
        var connection = new Async.SqliteConnection();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Join2(connection).SelectPartialAsync("opt_products.name", new CommandOptions(transaction: new StubTransaction())));
    }

    // ------------------------------------------------------------------
    // AUD-R38-041: the single-table aggregate and SelectPartial terminals.
    // ------------------------------------------------------------------

    private static readonly Dictionary<string, Action<IDbConnection, CommandOptions>> QueryTerminals = new()
    {
        ["SelectPartial(string)"] = (c, o) => c.From<Product>().SelectPartial(o, "name"),
        ["SelectPartial(expr)"] = (c, o) => c.From<Product>().SelectPartial(o, p => p.Name),
        ["SelectPartialFirst(string)"] = (c, o) => c.From<Product>().SelectPartialFirst(o, "name"),
        ["SelectPartialFirst(expr)"] = (c, o) => c.From<Product>().SelectPartialFirst(o, p => p.Name),
        ["SelectPartialFirstOrDefault(string)"] = (c, o) => c.From<Product>().SelectPartialFirstOrDefault(o, "name"),
        ["SelectPartialFirstOrDefault(expr)"] = (c, o) => c.From<Product>().SelectPartialFirstOrDefault(o, p => p.Name),
        ["SelectPartialSingle(string)"] = (c, o) => c.From<Product>().SelectPartialSingle(o, "name"),
        ["SelectPartialSingle(expr)"] = (c, o) => c.From<Product>().SelectPartialSingle(o, p => p.Name),
        ["SelectPartialSingleOrDefault(string)"] = (c, o) => c.From<Product>().SelectPartialSingleOrDefault(o, "name"),
        ["SelectPartialSingleOrDefault(expr)"] = (c, o) => c.From<Product>().SelectPartialSingleOrDefault(o, p => p.Name),
        ["Count"] = (c, o) => c.From<Product>().Count(p => p.Id, o),
        ["LongCount"] = (c, o) => c.From<Product>().LongCount(p => p.Id, o),
        ["Sum"] = (c, o) => c.From<Product>().Sum(p => p.Id, o),
        ["Avg"] = (c, o) => c.From<Product>().Avg(p => p.Id, o),
        ["Min"] = (c, o) => c.From<Product>().Min(p => p.Id, o),
        ["Max"] = (c, o) => c.From<Product>().Max(p => p.Id, o),
        ["SelectCount"] = (c, o) => c.From<Product>().SelectCount(p => p.Id, o),
        ["SelectSum"] = (c, o) => c.From<Product>().SelectSum(p => p.Id, o),
        ["SelectAvg"] = (c, o) => c.From<Product>().SelectAvg(p => p.Id, o),
        ["SelectMin"] = (c, o) => c.From<Product>().SelectMin(p => p.Id, o),
        ["SelectMax"] = (c, o) => c.From<Product>().SelectMax(p => p.Id, o),
        ["SelectCount()"] = (c, o) => c.From<Product>().SelectCount(o),
    };

    private static readonly Dictionary<string, Func<DbConnection, CommandOptions, Task>> QueryAsyncTerminals = new()
    {
        ["SelectPartialAsync(string)"] = (c, o) => c.From<Product>().SelectPartialAsync(["name"], o),
        ["SelectPartialAsync(expr)"] = (c, o) => c.From<Product>().SelectPartialAsync([p => p.Name], o),
        ["SelectPartialFirstAsync(string)"] = (c, o) => c.From<Product>().SelectPartialFirstAsync(["name"], o),
        ["SelectPartialFirstAsync(expr)"] = (c, o) => c.From<Product>().SelectPartialFirstAsync([p => p.Name], o),
        ["SelectPartialFirstOrDefaultAsync(string)"] = (c, o) => c.From<Product>().SelectPartialFirstOrDefaultAsync(["name"], o),
        ["SelectPartialFirstOrDefaultAsync(expr)"] = (c, o) => c.From<Product>().SelectPartialFirstOrDefaultAsync([p => p.Name], o),
        ["SelectPartialSingleAsync(string)"] = (c, o) => c.From<Product>().SelectPartialSingleAsync(["name"], o),
        ["SelectPartialSingleAsync(expr)"] = (c, o) => c.From<Product>().SelectPartialSingleAsync([p => p.Name], o),
        ["SelectPartialSingleOrDefaultAsync(string)"] = (c, o) => c.From<Product>().SelectPartialSingleOrDefaultAsync(["name"], o),
        ["SelectPartialSingleOrDefaultAsync(expr)"] = (c, o) => c.From<Product>().SelectPartialSingleOrDefaultAsync([p => p.Name], o),
        ["CountAsync"] = (c, o) => c.From<Product>().CountAsync(p => p.Id, o),
        ["LongCountAsync"] = (c, o) => c.From<Product>().LongCountAsync(p => p.Id, o),
        ["SumAsync"] = (c, o) => c.From<Product>().SumAsync(p => p.Id, o),
        ["AvgAsync"] = (c, o) => c.From<Product>().AvgAsync(p => p.Id, o),
        ["MinAsync"] = (c, o) => c.From<Product>().MinAsync(p => p.Id, o),
        ["MaxAsync"] = (c, o) => c.From<Product>().MaxAsync(p => p.Id, o),
        ["SelectCountAsync"] = (c, o) => c.From<Product>().SelectCountAsync(p => p.Id, o),
        ["SelectSumAsync"] = (c, o) => c.From<Product>().SelectSumAsync(p => p.Id, o),
        ["SelectAvgAsync"] = (c, o) => c.From<Product>().SelectAvgAsync(p => p.Id, o),
        ["SelectMinAsync"] = (c, o) => c.From<Product>().SelectMinAsync(p => p.Id, o),
        ["SelectMaxAsync"] = (c, o) => c.From<Product>().SelectMaxAsync(p => p.Id, o),
        ["SelectCountAsync()"] = (c, o) => c.From<Product>().SelectCountAsync(o),
    };

    public static TheoryData<string> QueryTerminalNames() => new(QueryTerminals.Keys);

    public static TheoryData<string> QueryAsyncTerminalNames() => new(QueryAsyncTerminals.Keys);

    [Theory]
    [MemberData(nameof(QueryTerminalNames))]
    public void QueryTerminal_WithOptions_AppliesTheTransactionAndTimeout(string terminal)
    {
        var connection = new SqliteConnection();
        var transaction = new StubTransaction();

        try
        {
            QueryTerminals[terminal](connection, new CommandOptions(transaction: transaction, commandTimeout: 47));
        }
        catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
        {
        }

        Assert.Same(transaction, connection.LastCommand!.Transaction);
        Assert.Equal(47, connection.LastCommand.CommandTimeout);
    }

    [Theory]
    [MemberData(nameof(QueryAsyncTerminalNames))]
    public async Task QueryTerminalAsync_WithOptions_AppliesTheTransactionAndTimeout(string terminal)
    {
        var connection = new Async.SqliteConnection();
        DbTransaction transaction = connection.BeginTransaction();

        try
        {
            await QueryAsyncTerminals[terminal](connection, new CommandOptions(transaction: transaction, commandTimeout: 48));
        }
        catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
        {
        }

        Assert.Same(transaction, connection.LastCommand!.Transaction);
        Assert.Equal(48, connection.LastCommand.CommandTimeout);
    }

    // ------------------------------------------------------------------
    // Stubs. Both connection classes must be named SqliteConnection: dialect resolution keys on
    // the connection's exact Type.Name, and Type.Name ignores the enclosing type, which is why the
    // async one is nested a level deeper rather than renamed.
    // ------------------------------------------------------------------

    private sealed class StubTransaction : IDbTransaction
    {
        public IDbConnection? Connection => null;
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public void Commit() { }
        public void Dispose() { }
        public void Rollback() { }
    }

    private sealed class SqliteConnection : IDbConnection
    {
        public CapturingCommand? LastCommand { get; private set; }

        #pragma warning disable CS8767 // IDbConnection.ConnectionString is [AllowNull]; the attribute is not public on net472.
        public string ConnectionString { get; set; } = "";
        #pragma warning restore CS8767
        public int ConnectionTimeout => 0;
        public string Database => "";
        public ConnectionState State => ConnectionState.Open;

        public IDbTransaction BeginTransaction() => new StubTransaction();
        public IDbTransaction BeginTransaction(IsolationLevel il) => new StubTransaction();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public void Dispose() { }
        public void Open() { }

        public IDbCommand CreateCommand() => LastCommand = new CapturingCommand();
    }

    private sealed class CapturingCommand : IDbCommand
    {
        private CommandType _commandType = CommandType.Text;

        #pragma warning disable CS8767 // IDbCommand.CommandText is [AllowNull]; the attribute is not public on net472.
        public string CommandText { get; set; } = "";
        #pragma warning restore CS8767
        public int CommandTimeout { get; set; }

        /// <summary>Whether anything assigned <see cref="CommandType"/> at all, as distinct from
        /// assigning it the value it already had.</summary>
        public bool CommandTypeWasSet { get; private set; }

        public CommandType CommandType
        {
            get => _commandType;
            set { _commandType = value; CommandTypeWasSet = true; }
        }

        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters { get; } = new CapturingParameters();
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => new CapturingParameter();
        public void Dispose() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => new EmptyReader();
        public IDataReader ExecuteReader(CommandBehavior behavior) => new EmptyReader();
        public object? ExecuteScalar() => 0;
        public void Prepare() { }
    }

    private sealed class CapturingParameters : List<object>, IDataParameterCollection
    {
        public object this[string parameterName] { get => this[0]; set { } }
        public bool Contains(string parameterName) => false;
        public int IndexOf(string parameterName) => -1;
        public void RemoveAt(string parameterName) { }
    }

    private sealed class CapturingParameter : IDbDataParameter
    {
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => true;
        #pragma warning disable CS8767 // IDataParameter.ParameterName is [AllowNull]; the attribute is not public on net472. / // IDataParameter.SourceColumn is [AllowNull]; the attribute is not public on net472.
        public string ParameterName { get; set; } = "";
        public string SourceColumn { get; set; } = "";
        #pragma warning restore CS8767
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
    }

    private sealed class EmptyReader : IDataReader
    {
        public int Depth => 0;
        public bool IsClosed => false;
        public int RecordsAffected => 0;
        public int FieldCount => 0;

        public bool Read() => false;
        public bool NextResult() => false;
        public void Close() { }
        public void Dispose() { }
        public DataTable? GetSchemaTable() => null;

        public object this[int i] => 0;
        public object this[string name] => 0;
        public bool GetBoolean(int i) => false;
        public byte GetByte(int i) => 0;
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => 0;
        public char GetChar(int i) => '\0';
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => 0;
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public string GetDataTypeName(int i) => "int";
        public DateTime GetDateTime(int i) => default;
        public decimal GetDecimal(int i) => 0;
        public double GetDouble(int i) => 0;
        public Type GetFieldType(int i) => typeof(int);
        public float GetFloat(int i) => 0;
        public Guid GetGuid(int i) => default;
        public short GetInt16(int i) => 0;
        public int GetInt32(int i) => 0;
        public long GetInt64(int i) => 0;
        public string GetName(int i) => "";
        public int GetOrdinal(string name) => -1;
        public string GetString(int i) => "";
        public object GetValue(int i) => 0;
        public int GetValues(object[] values) => 0;
        public bool IsDBNull(int i) => true;
    }

    /// <summary>
    /// The async counterparts require a <see cref="DbConnection"/>. Nested one level deeper only so
    /// this class can also be called <c>SqliteConnection</c> - two sibling nested types cannot share
    /// a name, and dialect resolution needs that exact name.
    /// </summary>
    private static class Async
    {
        internal sealed class SqliteConnection : DbConnection
        {
            public CapturingDbCommand? LastCommand { get; private set; }

            #pragma warning disable CS8765 // DbConnection.ConnectionString is [AllowNull]; the attribute is not public on net472.
            public override string ConnectionString { get; set; } = "";
            #pragma warning restore CS8765
            public override string Database => "";
            public override string DataSource => "";
            public override string ServerVersion => "";
            public override ConnectionState State => ConnectionState.Open;

            public override void ChangeDatabase(string databaseName) { }
            public override void Close() { }
            public override void Open() { }

            public new DbTransaction BeginTransaction() => new CapturingDbTransaction(this);

            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
                => new CapturingDbTransaction(this);

            protected override DbCommand CreateDbCommand() => LastCommand = new CapturingDbCommand();
        }

        internal sealed class CapturingDbTransaction(DbConnection connection) : DbTransaction
        {
            protected override DbConnection DbConnection { get; } = connection;
            public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
            public override void Commit() { }
            public override void Rollback() { }
        }

        internal sealed class CapturingDbCommand : DbCommand
        {
            #pragma warning disable CS8765 // DbCommand.CommandText is [AllowNull]; the attribute is not public on net472.
            public override string CommandText { get; set; } = "";
            #pragma warning restore CS8765
            public override int CommandTimeout { get; set; }
            public override CommandType CommandType { get; set; } = CommandType.Text;
            public override bool DesignTimeVisible { get; set; }
            public override UpdateRowSource UpdatedRowSource { get; set; }

            protected override DbConnection? DbConnection { get; set; }
            protected override DbParameterCollection DbParameterCollection { get; } = new CapturingDbParameters();
            protected override DbTransaction? DbTransaction { get; set; }

            public override void Cancel() { }
            public override int ExecuteNonQuery() => 0;
            public override object? ExecuteScalar() => 0;
            public override void Prepare() { }

            protected override DbParameter CreateDbParameter() => new CapturingDbParameter();
            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new EmptyDbReader();
        }

        internal sealed class CapturingDbParameters : DbParameterCollection
        {
            private readonly List<DbParameter> _items = [];

            public override int Count => _items.Count;
            public override object SyncRoot { get; } = new();

            public override int Add(object value) { _items.Add((DbParameter)value); return _items.Count - 1; }
            public override void AddRange(Array values) { foreach (object v in values) Add(v); }
            public override void Clear() => _items.Clear();
            public override bool Contains(object value) => _items.Contains((DbParameter)value);
            public override bool Contains(string value) => false;
            public override void CopyTo(Array array, int index) { }
            public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
            public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
            public override int IndexOf(string parameterName) => -1;
            public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
            public override void Remove(object value) => _items.Remove((DbParameter)value);
            public override void RemoveAt(int index) => _items.RemoveAt(index);
            public override void RemoveAt(string parameterName) { }

            protected override DbParameter GetParameter(int index) => _items[index];
            protected override DbParameter GetParameter(string parameterName) => _items[0];
            protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
            protected override void SetParameter(string parameterName, DbParameter value) { }
        }

        internal sealed class CapturingDbParameter : DbParameter
        {
            public override DbType DbType { get; set; }
            public override ParameterDirection Direction { get; set; }
            public override bool IsNullable { get; set; }
            #pragma warning disable CS8765 // DbParameter.ParameterName is [AllowNull]; the attribute is not public on net472.
            public override string ParameterName { get; set; } = "";
            #pragma warning restore CS8765
            public override int Size { get; set; }
            #pragma warning disable CS8765 // DbParameter.SourceColumn is [AllowNull]; the attribute is not public on net472.
            public override string SourceColumn { get; set; } = "";
            #pragma warning restore CS8765
            public override bool SourceColumnNullMapping { get; set; }
            public override object? Value { get; set; }

            public override void ResetDbType() { }
        }

        internal sealed class EmptyDbReader : DbDataReader
        {
            public override int Depth => 0;
            public override int FieldCount => 0;
            public override bool HasRows => false;
            public override bool IsClosed => false;
            public override int RecordsAffected => 0;

            public override object this[int ordinal] => 0;
            public override object this[string name] => 0;

            public override bool GetBoolean(int ordinal) => false;
            public override byte GetByte(int ordinal) => 0;
            public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
            public override char GetChar(int ordinal) => '\0';
            public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
            public override string GetDataTypeName(int ordinal) => "int";
            public override DateTime GetDateTime(int ordinal) => default;
            public override decimal GetDecimal(int ordinal) => 0;
            public override double GetDouble(int ordinal) => 0;
            public override System.Collections.IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
            public override Type GetFieldType(int ordinal) => typeof(int);
            public override float GetFloat(int ordinal) => 0;
            public override Guid GetGuid(int ordinal) => default;
            public override short GetInt16(int ordinal) => 0;
            public override int GetInt32(int ordinal) => 0;
            public override long GetInt64(int ordinal) => 0;
            public override string GetName(int ordinal) => "";
            public override int GetOrdinal(string name) => -1;
            public override string GetString(int ordinal) => "";
            public override object GetValue(int ordinal) => 0;
            public override int GetValues(object[] values) => 0;
            public override bool IsDBNull(int ordinal) => true;
            public override bool NextResult() => false;
            public override bool Read() => false;
        }
    }
}
