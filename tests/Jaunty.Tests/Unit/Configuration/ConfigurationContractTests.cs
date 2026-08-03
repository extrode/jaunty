using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Interceptors;
using Jaunty.Interfaces;

using Xunit;

namespace Jaunty.Tests.Unit.Configuration;

/// <summary>
/// Round 35, batch 06b. Six settings and two guards across
/// <see cref="JauntyConfig"/>, <see cref="BulkCopyConfiguration"/>, <see cref="LoggingConfiguration"/>
/// and <see cref="TableAttribute"/> that were either unvalidated or untested - in three cases
/// re-reported unfixed since round 33.
/// </summary>
[Collection("Type Handler Operations")]
public class ConfigurationContractTests : IDisposable
{
    private readonly int _parameterParsingCapacity = JauntyConfig.ParameterParsingCapacity;
    private readonly int _csvFieldCapacity = JauntyConfig.CsvFieldCapacity;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        JauntyConfig.ParameterParsingCapacity = _parameterParsingCapacity;
        JauntyConfig.CsvFieldCapacity = _csvFieldCapacity;
        JauntyConfig.ClearInterceptors();
        BulkCopyConfiguration.Reset();
    }

    private sealed class NoopInterceptor : ICommandInterceptor
    {
        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken = default) =>
            default;

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken = default) =>
            default;

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken = default) =>
            default;
    }

    // ------------------------------------------------------------------
    // AUD-R35-145: the clamping capacities, unverified since round 9
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ANonPositiveParameterParsingCapacity_FallsBackToTheDefault(int value)
    {
        JauntyConfig.ParameterParsingCapacity = value;

        Assert.Equal(8, JauntyConfig.ParameterParsingCapacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ANonPositiveCsvFieldCapacity_FallsBackToTheDefault(int value)
    {
        JauntyConfig.CsvFieldCapacity = value;

        Assert.Equal(16, JauntyConfig.CsvFieldCapacity);
    }

    [Fact]
    public void APositiveCapacity_IsKept()
    {
        JauntyConfig.ParameterParsingCapacity = 3;
        JauntyConfig.CsvFieldCapacity = 40;

        Assert.Equal(3, JauntyConfig.ParameterParsingCapacity);
        Assert.Equal(40, JauntyConfig.CsvFieldCapacity);
    }

    // ------------------------------------------------------------------
    // AUD-R35-146: AddInterceptorIfNotPresent's own contract
    // ------------------------------------------------------------------

    [Fact]
    public void AddInterceptorIfNotPresent_ReportsWhetherItAdded()
    {
        JauntyConfig.ClearInterceptors();
        var interceptor = new NoopInterceptor();

        Assert.True(JauntyConfig.AddInterceptorIfNotPresent(interceptor));
        Assert.False(JauntyConfig.AddInterceptorIfNotPresent(interceptor));
    }

    [Fact]
    public void AddInterceptorIfNotPresent_ComparesByReference_NotByType()
    {
        JauntyConfig.ClearInterceptors();

        Assert.True(JauntyConfig.AddInterceptorIfNotPresent(new NoopInterceptor()));
        Assert.True(JauntyConfig.AddInterceptorIfNotPresent(new NoopInterceptor()));
    }

    [Fact]
    public void AddInterceptorIfNotPresent_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => JauntyConfig.AddInterceptorIfNotPresent(null!));
    }

    // ------------------------------------------------------------------
    // AUD-R35-142: a null element in the sequence
    // ------------------------------------------------------------------

    [Fact]
    public void AddInterceptors_RejectsANullElement_AtTheRegistrationSite()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => JauntyConfig.AddInterceptors([new NoopInterceptor(), null!]));

        Assert.Equal("interceptors", ex.ParamName);
    }

    [Fact]
    public void AddInterceptors_RejectsANullSequence()
    {
        Assert.Throws<ArgumentNullException>(() => JauntyConfig.AddInterceptors(null!));
    }

    /// <summary>
    /// The rejection must be total: a sequence with a bad element registers nothing, rather than
    /// leaving the interceptors before it installed.
    /// </summary>
    [Fact]
    public void AddInterceptors_WithABadElement_RegistersNothing()
    {
        JauntyConfig.ClearInterceptors();

        Assert.Throws<ArgumentNullException>(
            () => JauntyConfig.AddInterceptors([new NoopInterceptor(), null!]));

        Assert.True(JauntyConfig.AddInterceptorIfNotPresent(new NoopInterceptor()));
    }

    // ------------------------------------------------------------------
    // AUD-R35-144: BulkCopyConfiguration validates at the setter
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ABatchSizeThatIsNotPositive_IsRejected(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BulkCopyConfiguration.DefaultBatchSize = value);
        Assert.Equal(10000, BulkCopyConfiguration.DefaultBatchSize);
    }

    [Fact]
    public void ANegativeTimeout_IsRejected_ButZeroMeansNoTimeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BulkCopyConfiguration.DefaultTimeout = -1);

        BulkCopyConfiguration.DefaultTimeout = 0;
        Assert.Equal(0, BulkCopyConfiguration.DefaultTimeout);
    }

    [Fact]
    public void ANegativeNativeBulkCopyThreshold_IsRejected_ButZeroIsValid()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = -1);

        BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = 0;
        Assert.Equal(0, BulkCopyConfiguration.MinimumRowsForNativeBulkCopy);
    }

    [Fact]
    public void AnUndefinedIdentityMode_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BulkCopyConfiguration.DefaultIdentityMode = (BulkCopyIdentityMode)9);

        Assert.Equal(BulkCopyIdentityMode.Default, BulkCopyConfiguration.DefaultIdentityMode);
    }

    [Fact]
    public void TheValidSettingsRoundTrip()
    {
        BulkCopyConfiguration.DefaultBatchSize = 500;
        BulkCopyConfiguration.DefaultIdentityMode = BulkCopyIdentityMode.KeepIdentity;
        BulkCopyConfiguration.EnableNativeBulkCopy = false;
        BulkCopyConfiguration.DefaultCheckConstraints = false;

        Assert.Equal(500, BulkCopyConfiguration.DefaultBatchSize);
        Assert.Equal(BulkCopyIdentityMode.KeepIdentity, BulkCopyConfiguration.DefaultIdentityMode);
        Assert.False(BulkCopyConfiguration.EnableNativeBulkCopy);
        Assert.False(BulkCopyConfiguration.DefaultCheckConstraints);

        BulkCopyConfiguration.Reset();

        Assert.Equal(10000, BulkCopyConfiguration.DefaultBatchSize);
        Assert.Equal(BulkCopyIdentityMode.Default, BulkCopyConfiguration.DefaultIdentityMode);
        Assert.True(BulkCopyConfiguration.EnableNativeBulkCopy);
        Assert.True(BulkCopyConfiguration.DefaultCheckConstraints);
    }

    // ------------------------------------------------------------------
    // AUD-R35-147: MaskedValueFormat, unasserted since round 34
    // ------------------------------------------------------------------

    [Fact]
    public void TheMaskedValueFormat_HasADefault_AndIsSettable()
    {
        var configuration = new LoggingConfiguration();

        Assert.Equal("***MASKED***", configuration.MaskedValueFormat);

        configuration.MaskedValueFormat = "[redacted]";
        Assert.Equal("[redacted]", configuration.MaskedValueFormat);
    }

    // ------------------------------------------------------------------
    // AUD-R35-148: TableAttribute's constructor
    // ------------------------------------------------------------------

    [Fact]
    public void ATableNameIsRequired()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new TableAttribute(null!));

        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void ATableSchemaDefaultsToNull_AndIsKeptWhenGiven()
    {
        Assert.Null(new TableAttribute("orders").Schema);
        Assert.Equal("sales", new TableAttribute("orders", "sales").Schema);
        Assert.Equal("orders", new TableAttribute("orders", "sales").Name);
    }
}
