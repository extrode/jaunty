using System.Data;
using Jaunty.Core;

namespace Jaunty.Tests.Unit;

public class CommandOptionsTests
{
    [Fact]
    public void Default_HasNullValues()
    {
        var options = default(CommandOptions);

        Assert.Null(options.Transaction);
        Assert.Null(options.CommandTimeout);
    }

    [Fact]
    public void Constructor_SetsValues()
    {
        var options = new CommandOptions(null, 30);

        Assert.Null(options.Transaction);
        Assert.Equal(30, options.CommandTimeout);
    }

    [Fact]
    public void WithTimeout_SetsTimeoutOnly()
    {
        var options = CommandOptions.WithTimeout(60);

        Assert.Null(options.Transaction);
        Assert.Equal(60, options.CommandTimeout);
    }

    [Fact]
    public void WithTransaction_SetsTransactionOnly()
    {
        var transaction = new TestDbTransaction();

        var options = CommandOptions.WithTransaction(transaction);

        Assert.Same(transaction, options.Transaction);
        Assert.Null(options.CommandTimeout);
        Assert.Equal(CommandType.Text, options.CommandType);
    }

    [Fact]
    public void With_SetsBothValues()
    {
        var transaction = new TestDbTransaction();

        var options = CommandOptions.With(transaction, 45);

        Assert.Same(transaction, options.Transaction);
        Assert.Equal(45, options.CommandTimeout);
    }

    [Fact]
    public void IsReadonlyStruct()
    {
        var type = typeof(CommandOptions);

        Assert.True(type.IsValueType);
        // Verify it's readonly by checking the fields
        var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        Assert.All(fields, f => Assert.True(f.IsInitOnly || !f.IsPublic));
    }

    private class TestDbTransaction : IDbTransaction
    {
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public IDbConnection? Connection { get; set; }
        public void Commit() { }
        public void Rollback() { }
        public void Dispose() { }
    }
}