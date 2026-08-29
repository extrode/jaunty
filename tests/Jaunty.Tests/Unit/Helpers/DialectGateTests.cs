using System.Reflection;

using Jaunty.Tests.Helpers.Dialects;

using Xunit.Sdk;
using Xunit.v3;

namespace Jaunty.Tests.Unit.Helpers;

/// <summary>
/// The skip-or-fail decision every dialect attribute makes, tested directly rather than through a
/// live engine. The end-to-end behaviour was measured on 2026-08-30 against a dead port, but two
/// of the five cases cannot be reached that way: "not configured" needs
/// <see cref="Jaunty.Tests.Helpers.TestConfiguration"/> to find no connection string at all, and
/// it walks six directories up looking for an <c>appsettings.json</c> that exists on any machine
/// set up to run these tests.
/// </summary>
public class DialectGateTests
{
    private sealed class FakeDialectAttribute : DialectDataAttributeBase
    {
        private readonly bool _configured;
        private readonly bool _reachable;
        private readonly string? _requireVariable;

        public FakeDialectAttribute(bool configured, bool reachable, string? requireVariable = null)
            : base("not configured")
        {
            _configured = configured;
            _reachable = reachable;
            _requireVariable = requireVariable;
            ApplySkipIfUnavailable();
        }

        protected override bool IsAvailable => _configured;

        protected override bool IsReachable => _reachable;

        protected override string UnreachableReason => "the server did not answer";

        protected override string? RequireVariable => _requireVariable;

        protected override DialectInfo Dialect => DialectInfo.SqlServer;
    }

    private static string SetRequire(string value, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        string variable = $"JAUNTY_REQUIRE_FAKE_{caller}";
        Environment.SetEnvironmentVariable(variable, value);
        return variable;
    }

    [Fact]
    public void Configured_AndReachable_DoesNotSkip()
    {
        var attribute = new FakeDialectAttribute(configured: true, reachable: true);

        Assert.Null(attribute.Skip);
    }

    [Fact]
    public void Configured_ButUnreachable_SkipsAndSaysWhy()
    {
        var attribute = new FakeDialectAttribute(configured: true, reachable: false);

        Assert.Contains("not reachable", attribute.Skip!, StringComparison.Ordinal);
        Assert.Contains("the server did not answer", attribute.Skip!, StringComparison.Ordinal);
    }

    [Fact]
    public void NotConfigured_SkipsWithTheConfigurationMessage()
    {
        var attribute = new FakeDialectAttribute(configured: false, reachable: false);

        Assert.Equal("not configured", attribute.Skip);
    }

    /// <summary>
    /// The CI case: a service container that failed to start must not read as a green leg.
    /// </summary>
    [Fact]
    public void Unreachable_ButRequired_DoesNotSkip()
    {
        string variable = SetRequire("1");

        var attribute = new FakeDialectAttribute(configured: true, reachable: false, variable);

        Assert.Null(attribute.Skip);
    }

    [Fact]
    public void NotConfigured_ButRequired_DoesNotSkip()
    {
        string variable = SetRequire("1");

        var attribute = new FakeDialectAttribute(configured: false, reachable: false, variable);

        Assert.Null(attribute.Skip);
    }

    [Fact]
    public void Required_IsIgnoredWhenTheEngineIsPresent()
    {
        string variable = SetRequire("1");

        var attribute = new FakeDialectAttribute(configured: true, reachable: true, variable);

        Assert.Null(attribute.Skip);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("FALSE")]
    [InlineData("no")]
    [InlineData("")]
    [InlineData("   ")]
    public void AnOffValue_LeavesTheDialectSkippable(string value)
    {
        string variable = SetRequire(value);

        var attribute = new FakeDialectAttribute(configured: true, reachable: false, variable);

        Assert.NotNull(attribute.Skip);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("yes")]
    [InlineData("on")]
    public void AnOnValue_MakesTheDialectRequired(string value)
    {
        string variable = SetRequire(value);

        var attribute = new FakeDialectAttribute(configured: true, reachable: false, variable);

        Assert.Null(attribute.Skip);
    }

    [Fact]
    public void AnUnsetVariable_LeavesTheDialectSkippable()
    {
        var attribute = new FakeDialectAttribute(
            configured: true, reachable: false, "JAUNTY_REQUIRE_FAKE_NEVER_SET_ANYWHERE");

        Assert.NotNull(attribute.Skip);
    }

    /// <summary>
    /// A required-but-absent engine must fail only its own rows. Throwing from
    /// <see cref="DataAttribute.GetData"/> would fail the whole test method and take every other
    /// dialect's row with it - measured as total 11 rather than 55 on
    /// <c>Integration/Get/GetTests</c> before this was changed.
    /// </summary>
    [Fact]
    public async Task AMissingRequiredEngine_StillYieldsItsRow()
    {
        string variable = SetRequire("1");
        var attribute = new FakeDialectAttribute(configured: false, reachable: false, variable);

        MethodInfo method = typeof(DialectGateTests).GetMethod(
            nameof(AMissingRequiredEngine_StillYieldsItsRow),
            BindingFlags.Public | BindingFlags.Instance)!;

        await using var tracker = new DisposalTracker();
        IReadOnlyCollection<ITheoryDataRow> rows = await attribute.GetData(method, tracker);

        Assert.Single(rows);
    }
}
