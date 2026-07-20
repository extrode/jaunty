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

    [Fact]
    public void ExplicitZeroBatchSize_IsRespectedRatherThanFallingBackToDefault()
    {
        // An explicit 0 must be distinguished from "unset" (default(ImportOptions)) — the fallback
        // to 1000 should only apply when the struct was never constructed at all.
        var options = new ImportOptions(batchSize: 0);

        Assert.Equal(0, options.BatchSize);
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
