using Jaunty.Internals.Write;

namespace Jaunty.Tests.Unit.Write;

/// <summary>
/// AUD-R35-127. <c>BulkOperationParameters</c> is the whole of what an interceptor is told about a
/// bulk write, and its three properties had never been read by a test - only the string
/// <c>ToString()</c> produces, and only for <c>BulkInsert</c> and <c>ExecuteBatch</c>. An
/// interceptor that reads <c>Operation</c> or <c>RowCount</c> off the reported object rather than
/// parsing the description was relying on untested behaviour.
/// </summary>
public class BulkOperationParametersTests
{
    private sealed class Widget;

    [Fact]
    public void TheEntityTypedConstructor_ReportsTheTypeName_NotTheFullName()
    {
        var parameters = new BulkOperationParameters("BulkInsert", typeof(Widget), 7);

        Assert.Equal("BulkInsert", parameters.Operation);
        Assert.Equal(nameof(Widget), parameters.EntityType);
        Assert.Equal(7, parameters.RowCount);
    }

    [Fact]
    public void TheUntypedConstructor_KeepsWhatItIsGiven()
    {
        var parameters = new BulkOperationParameters("ExecuteBatch", (string?)null, (int?)null);

        Assert.Equal("ExecuteBatch", parameters.Operation);
        Assert.Null(parameters.EntityType);
        Assert.Null(parameters.RowCount);
    }

    [Theory]
    [InlineData("BulkInsert", "Widget", 7, "BulkInsert<Widget> x7")]
    [InlineData("BulkDelete", "Widget", 0, "BulkDelete<Widget> x0")]
    [InlineData("ExecuteBatch", null, 2, "ExecuteBatch x2")]
    [InlineData("ExecuteBatch", null, null, "ExecuteBatch")]
    [InlineData("Import", "Order", null, "Import<Order>")]
    public void ToString_ComposesOnlyTheParts_ItActuallyKnows(string operation, string? entityType, int? rowCount, string expected)
    {
        var parameters = new BulkOperationParameters(operation, entityType, rowCount);

        Assert.Equal(expected, parameters.ToString());
    }

    /// <summary>
    /// A zero row count is a fact, not an absence: an interceptor filtering on "did this write
    /// anything" must be able to tell <c>x0</c> from a count nobody could work out.
    /// </summary>
    [Fact]
    public void AZeroRowCount_IsReported_WhileAnUnknownOneIsOmitted()
    {
        Assert.Equal("BulkUpdate<Widget> x0", new BulkOperationParameters("BulkUpdate", "Widget", 0).ToString());
        Assert.Equal("BulkUpdate<Widget>", new BulkOperationParameters("BulkUpdate", "Widget", null).ToString());
    }
}
