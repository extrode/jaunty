using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Tests.Helpers;
using Extrode.Jaunty.TypeHandlers;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.TypeHandlers;

/// <summary>
/// AUD-R35-174. <see cref="TypeHandler{T}.Parse"/> was declared to return a non-nullable
/// <c>T</c> while its own documentation said the result may be null and
/// the registry's since-removed <c>TryConvertFromDb</c> had a dedicated branch for a handler that
/// returns one. A reference-type handler written the documented way therefore produced CS8603 at
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
        TestInitializer.Initialize();
    }

    private sealed class NullReturningStringHandler : TypeHandler<string>
    {
        public override string? Parse(object? dbValue) =>
            dbValue is null or DBNull ? null : dbValue.ToString();

        public override object? ToDbValue(string? value) => value;
    }

    [Fact]
    public void AReferenceHandlerMayReturnNullForADatabaseNull()
    {
        JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler(new NullReturningStringHandler()));

        Assert.True(TypeHandlerRegistry.TryGetHandler(typeof(string), out ITypeHandler? handler));
        Assert.Null(handler!.Parse(DBNull.Value));
        Assert.Equal("abc", handler.Parse("abc"));
    }
}
