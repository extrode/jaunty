#if NET8_0_OR_GREATER
using System.Reflection;

using Jaunty.Configuration;
using Jaunty.Extensions.Reflection.BulkCopy;

using Microsoft.Data.SqlClient;

using Xunit;

namespace Jaunty.Tests.Unit.BulkCopy;

/// <summary>
/// AUD-R25 (B6-5): <c>BulkCopyOptions.EnableStreaming</c> was public, defaulted to
/// <see langword="true"/> and documented as "rows are streamed to the database without buffering" -
/// but no provider read it. The only references in the repository were its own declaration and
/// three get/set round-trip assertions in <c>BulkCopyConfigurationTests</c>; none of
/// <c>SqlServerBulkCopyProvider</c>, <c>PostgreSqlBulkCopyProvider</c> or
/// <c>MySqlBulkCopyProvider</c> consulted it, so setting it to <see langword="false"/> changed
/// nothing anywhere. Same species as <c>LoggingConfiguration.LogExecutionTime</c> (batch 4): a
/// documented knob with no wiring.
///
/// <para>
/// On SQL Server it maps directly onto <see cref="SqlBulkCopy.EnableStreaming"/>, which the
/// provider never set despite reflecting over five other <c>SqlBulkCopy</c> properties. It is now
/// applied on both the sync and async paths. The other two providers cannot offer the knob -
/// Postgres' binary COPY has no buffered mode, and MySQL always buffers a chunk at a time - so
/// <c>BulkCopyOptions.EnableStreaming</c> documents what each actually does rather than claiming a
/// setting it does not have.
/// </para>
///
/// <para>
/// Whether streaming was actually enabled is not observable from managed code - <c>SqlBulkCopy</c>
/// exposes no counter for it and the difference is in how rows are read from the source. What is
/// assertable, and what was actually missing, is the wiring: that the mapping target exists and
/// that the provider resolves it. The end-to-end behaviour is covered by the server-gated bulk-copy
/// integration tests, which run against a real SQL Server.
/// </para>
/// </summary>
public class EnableStreamingWiringTests
{
    [Fact]
    public void SqlBulkCopy_ExposesTheEnableStreamingProperty_TheOptionMapsOnto()
    {
        // If this ever fails the provider's reflection lookup silently returns null and the option
        // goes back to doing nothing, which is exactly how the original defect was invisible.
        PropertyInfo? property = typeof(SqlBulkCopy).GetProperty(nameof(SqlBulkCopy.EnableStreaming));

        Assert.NotNull(property);
        Assert.True(property!.CanWrite);
        Assert.Equal(typeof(bool), property.PropertyType);
    }

    [Fact]
    public void SqlServerBulkCopyProvider_ResolvesTheEnableStreamingProperty()
    {
        PropertyInfo? resolved = ResolvedEnableStreamingProperty();

        Assert.NotNull(resolved);
        Assert.Equal(typeof(SqlBulkCopy), resolved!.DeclaringType);
        Assert.Equal(nameof(SqlBulkCopy.EnableStreaming), resolved.Name);
    }

    [Fact]
    public void SqlServerBulkCopyProvider_TheResolvedPropertyActuallySetsTheValue()
    {
        // Resolving the PropertyInfo is only half of it - the provider then calls SetValue on a
        // SqlBulkCopy instance, so pin that the pairing works on a real one.
        PropertyInfo property = ResolvedEnableStreamingProperty()!;

        using var connection = new SqlConnection("Server=unused;Database=unused;Trusted_Connection=True;");
        using var bulkCopy = new SqlBulkCopy(connection);

        property.SetValue(bulkCopy, false);
        Assert.False(bulkCopy.EnableStreaming);

        property.SetValue(bulkCopy, true);
        Assert.True(bulkCopy.EnableStreaming);
    }

    [Fact]
    public void BulkCopyOptions_EnableStreaming_StillDefaultsToTrue()
    {
        // The default is now load-bearing on SQL Server rather than inert, so it is worth pinning:
        // flipping it would change behaviour for every caller who never set it.
        Assert.True(new BulkCopyOptions().EnableStreaming);
    }

    [Fact]
    public void BulkCopyOptions_EnableStreaming_RoundTrips()
    {
        var options = new BulkCopyOptions { EnableStreaming = false };

        Assert.False(options.EnableStreaming);
    }

    private static PropertyInfo? ResolvedEnableStreamingProperty()
    {
        FieldInfo? field = typeof(SqlServerBulkCopyProvider).GetField(
            "EnableStreamingProperty",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.True(field is not null,
            "SqlServerBulkCopyProvider no longer has an EnableStreamingProperty field - if the "
            + "provider was refactored, this test needs to follow it rather than be deleted.");

        return (PropertyInfo?)field!.GetValue(null);
    }
}
#endif
