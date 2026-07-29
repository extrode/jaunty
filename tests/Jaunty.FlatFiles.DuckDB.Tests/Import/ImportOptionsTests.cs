namespace Jaunty.FlatFiles.DuckDB.Tests.Import;

/// <summary>
/// Regression tests for <see cref="ImportOptions"/> (AUD-R9): <c>default(ImportOptions)</c> —
/// which bypasses the constructor and zero-initializes value-type fields — must still expose the
/// documented default <c>BatchSize</c> of 1000, not 0. <c>ImportIntoAsync</c>'s optional
/// <c>options</c> parameter is declared with <c>= default</c>, so this directly affects every call
/// that doesn't explicitly pass <see cref="ImportOptions"/>.
/// </summary>
public class ImportOptionsTests
{
    [Fact]
    public void Default_BatchSize_Is1000()
    {
        var options = default(ImportOptions);

        Assert.Equal(1000, options.BatchSize);
    }

    [Fact]
    public void ParameterlessConstructor_BatchSize_Is1000()
    {
        var options = new ImportOptions();

        Assert.Equal(1000, options.BatchSize);
    }

    [Fact]
    public void ExplicitBatchSize_IsRespected()
    {
        var options = new ImportOptions(batchSize: 250);

        Assert.Equal(250, options.BatchSize);
    }

    /// <summary>
    /// AUD-R26-064: this used to assert that an explicit 0 was <em>respected</em>, on the reasoning
    /// that 0 must stay distinguishable from "unset". The distinction is real and still holds - see
    /// the test below - but respecting the value was the wrong conclusion from it. Both import loops
    /// compare <c>rows &gt;= batchSize</c>, so 0 flushed after every single row: a batched import
    /// silently became a row-at-a-time one, with the progress callback firing per row, and the
    /// <c>DbBatch</c> path exists specifically to avoid that round-trip pattern. There is no reading
    /// of "batch size 0" that a caller could want, so it is now rejected where "unset" and
    /// "explicitly zero" are still telling apart.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NonPositiveBatchSize_IsRejected(int batchSize)
    {
        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ImportOptions(batchSize: batchSize));

        Assert.Equal("batchSize", ex.ParamName);
    }

    /// <summary>
    /// And the distinction the nullable backing field exists for is unchanged: a
    /// <c>default(ImportOptions)</c> bypasses the constructor entirely - so it never sees the new
    /// guard - and still falls back to 1000 rather than reporting the zero-initialized 0.
    /// </summary>
    [Fact]
    public void DefaultStruct_BypassesTheGuard_AndStillFallsBackToTheDocumentedDefault()
    {
        var options = default(ImportOptions);

        Assert.Equal(1000, options.BatchSize);
    }

    [Fact]
    public void OtherFields_DefaultToDocumentedValues()
    {
        var options = default(ImportOptions);

        Assert.Equal(ConflictStrategy.Error, options.OnConflict);
        Assert.False(options.CreateTableIfMissing);
        Assert.Null(options.OnProgress);
        Assert.Null(options.Dialect);
    }
}
