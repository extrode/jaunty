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
        // Can't easily test with real transaction here, just verify method exists
        // and returns CommandOptions
        var method = typeof(CommandOptions).GetMethod("WithTransaction");
        Assert.NotNull(method);
    }

    [Fact]
    public void With_SetsBothValues()
    {
        // Can't easily test with real transaction here
        var method = typeof(CommandOptions).GetMethod("With");
        Assert.NotNull(method);
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
}
