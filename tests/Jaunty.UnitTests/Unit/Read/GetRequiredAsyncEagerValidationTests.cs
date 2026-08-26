using System.Data;

using Jaunty.Attributes;
using Jaunty.Core;
using Jaunty.Interfaces;

using Xunit;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R35-047: the four <c>GetRequiredAsync</c> overloads were declared <c>async</c>, so their
/// argument validation surfaced as a faulted <see cref="ValueTask"/> on await rather than throwing
/// at the call site. Every other async entry point in <c>Get.cs</c> - <c>GetAsync</c> in both its
/// forms, and both options overloads - is a non-<c>async</c> method that throws eagerly, and
/// AUD-R25 B2-1 fixed the same shape on <c>ExecuteAsync</c>/<c>ExecuteBatchAsync</c>.
///
/// <para>
/// Because the methods under test are no longer <c>async</c>, <c>Assert.Throws</c> without an await
/// is itself the eagerness assertion: an <c>async</c> method would return a faulted task and
/// <c>Assert.Throws</c> would see no exception at all.
/// </para>
/// </summary>
public class GetRequiredAsyncEagerValidationTests
{
    [Table("required_rows")]
    private sealed class RequiredRow
    {
        [Key]
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    [Table("required_typed_rows")]
    private sealed class RequiredTypedRow : IEntity<int>
    {
        [Key]
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    [Table("required_string_key_rows")]
    private sealed class RequiredStringKeyRow : IEntity<string>
    {
        [Key]
        public string Id { get; set; } = "";

        public string? Name { get; set; }
    }

    // A non-DbConnection stub: the id check runs before the DbConnection type test, so nothing
    // under test ever touches the connection itself.
    private static IDbConnection Unopened() => new StubNonDbConnection();

    // ---------------------------------------------------------------------------
    // Null connection
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetRequiredAsync_NullConnection_ThrowsSynchronously()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(
            () => nullConnection!.GetRequiredAsync<RequiredRow>(1));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void GetRequiredAsync_WithOptions_NullConnection_ThrowsSynchronously()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(
            () => nullConnection!.GetRequiredAsync<RequiredRow>(1, new CommandOptions<RequiredRow>()));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void GetRequiredAsync_TypedKey_NullConnection_ThrowsSynchronously()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(
            () => nullConnection!.GetRequiredAsync<RequiredTypedRow, int>(1));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void GetRequiredAsync_TypedKey_WithOptions_NullConnection_ThrowsSynchronously()
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(
            () => nullConnection!.GetRequiredAsync<RequiredTypedRow, int>(
                1, new CommandOptions<RequiredTypedRow>()));

        Assert.Equal("connection", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // Null id
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetRequiredAsync_NullId_ThrowsSynchronously()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => Unopened().GetRequiredAsync<RequiredRow>(null!));

        Assert.Equal("id", ex.ParamName);
    }

    [Fact]
    public void GetRequiredAsync_WithOptions_NullId_ThrowsSynchronously()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => Unopened().GetRequiredAsync<RequiredRow>(null!, new CommandOptions<RequiredRow>()));

        Assert.Equal("id", ex.ParamName);
    }

    [Fact]
    public void GetRequiredAsync_TypedKey_NullId_ThrowsSynchronously()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => Unopened().GetRequiredAsync<RequiredStringKeyRow, string>(null!));

        Assert.Equal("id", ex.ParamName);
    }

    [Fact]
    public void GetRequiredAsync_TypedKey_WithOptions_NullId_ThrowsSynchronously()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => Unopened().GetRequiredAsync<RequiredStringKeyRow, string>(
                null!, new CommandOptions<RequiredStringKeyRow>()));

        Assert.Equal("id", ex.ParamName);
    }

    // ---------------------------------------------------------------------------
    // Non-DbConnection: the third eagerly validated condition
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetRequiredAsync_NonDbConnection_ThrowsSynchronously()
    {
        IDbConnection plain = new StubNonDbConnection();

        Assert.Throws<InvalidOperationException>(() => plain.GetRequiredAsync<RequiredRow>(1));
    }

    [Fact]
    public void GetRequiredAsync_TypedKey_NonDbConnection_ThrowsSynchronously()
    {
        IDbConnection plain = new StubNonDbConnection();

        Assert.Throws<InvalidOperationException>(() => plain.GetRequiredAsync<RequiredTypedRow, int>(1));
    }

    /// <summary>
    /// A typed key of a value type cannot be null, so nothing about that arm changes for it - the
    /// control for the two nullable-key cases above.
    /// </summary>
    [Fact]
    public void GetRequiredAsync_TypedValueTypeKey_ReachesTheConnectionCheck()
    {
        IDbConnection plain = new StubNonDbConnection();

        var ex = Assert.Throws<InvalidOperationException>(
            () => plain.GetRequiredAsync<RequiredTypedRow, int>(0));

        Assert.Contains("DbConnection", ex.Message, StringComparison.Ordinal);
    }

    private sealed class StubNonDbConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = "";

        public int ConnectionTimeout => 0;

        public string Database => "";

        public ConnectionState State => ConnectionState.Closed;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();

        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();

        public void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public void Close() { }

        public IDbCommand CreateCommand() => throw new NotSupportedException();

        public void Dispose() { }

        public void Open() => throw new NotSupportedException();
    }
}
