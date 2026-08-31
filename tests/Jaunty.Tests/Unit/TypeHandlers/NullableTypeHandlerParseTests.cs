using Jaunty.Configuration;
using Jaunty.TypeHandlers;

using Xunit;

namespace Jaunty.Tests.Unit.TypeHandlers;

/// <summary>
/// AUD-R35-174. <see cref="TypeHandler{T}.Parse"/> was declared to return a non-nullable
/// <c>T</c> while its own documentation said the result may be null and
/// <c>TypeHandlerRegistry.TryConvertFromDb</c> has a dedicated branch for a handler that returns one.
/// A reference-type handler taking the documented, registry-handled path therefore produced CS8603 at
/// the implementation site - this file is the compile-time half of the assertion as much as the
/// runtime half, because a handler written the documented way now compiles clean.
/// </summary>
[Collection("Type Handler Operations")]
public class NullableTypeHandlerParseTests : IDisposable
{
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        JauntyConfig.Reset();
    }

    private sealed class NullReturningStringHandler : TypeHandler<string>
    {
        public override string? Parse(object? dbValue) =>
            dbValue is null or DBNull ? null : dbValue.ToString();

        public override object? ToDbValue(string? value) => value;
    }

    private sealed class NullReturningIntHandler : TypeHandler<int>
    {
        public override int Parse(object? dbValue) => dbValue is null or DBNull ? 0 : (int)dbValue;

        public override object? ToDbValue(int value) => value;
    }

    [Fact]
    public void AReferenceHandlerMayReturnNullForADatabaseNull()
    {
        JauntyConfig.RegisterTypeHandler(new NullReturningStringHandler());

        Assert.True(TypeHandlerRegistry.TryConvertFromDb(DBNull.Value, out string? fromDbNull));
        Assert.Null(fromDbNull);

        Assert.True(TypeHandlerRegistry.TryConvertFromDb("abc", out string? fromValue));
        Assert.Equal("abc", fromValue);
    }

    /// <summary>
    /// The annotation change does not weaken AUD-R27-014: a null still cannot stand in for a
    /// non-nullable value type, because <c>default(T)</c> there is indistinguishable from real data.
    /// </summary>
    [Fact]
    public void AValueTypeHandlerStillCannotReportSuccessForANull()
    {
        JauntyConfig.RegisterTypeHandler(new NullReturningIntHandler());

        Assert.True(TypeHandlerRegistry.TryConvertFromDb(7, out int converted));
        Assert.Equal(7, converted);
    }
}
