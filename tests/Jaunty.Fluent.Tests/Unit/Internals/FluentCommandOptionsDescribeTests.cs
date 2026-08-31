using System.Data;

using Jaunty.Core;
using Jaunty.Fluent.Internals;

namespace Jaunty.Fluent.Tests.Unit.Internals;

/// <summary>
/// AUD-R35-200: <c>FluentCommandOptions.Describe</c> is what every fluent terminal reports to an
/// interceptor, and nothing asserted its output.
/// </summary>
public class FluentCommandOptionsDescribeTests
{
    [Fact]
    public void DefaultOptions_AreDescribedAsText()
    {
        Assert.Equal(CommandType.Text, FluentCommandOptions.Describe(default));
    }

    [Fact]
    public void NewOptions_AreDescribedAsText()
    {
        Assert.Equal(CommandType.Text, FluentCommandOptions.Describe(new CommandOptions()));
    }

    [Fact]
    public void AnUndefinedCommandTypeIsNormalisedToText()
    {
        var options = new CommandOptions(commandType: (CommandType)0);

        Assert.Equal(CommandType.Text, FluentCommandOptions.Describe(options));
    }

    [Theory]
    [InlineData(CommandType.StoredProcedure)]
    [InlineData(CommandType.TableDirect)]
    public void TheTwoAppliedCommandTypesArePassedThrough(CommandType commandType)
    {
        var options = new CommandOptions(commandType: commandType);

        Assert.Equal(commandType, FluentCommandOptions.Describe(options));
    }

    [Fact]
    public void AnExplicitTextCommandTypeIsDescribedAsText()
    {
        var options = new CommandOptions(commandType: CommandType.Text);

        Assert.Equal(CommandType.Text, FluentCommandOptions.Describe(options));
    }
}
