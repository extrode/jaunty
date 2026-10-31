using Extrode.Jaunty.FlatFiles;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Unit;

public class ImportOptionsConflictStrategyTests
{
    [Fact]
    public void UndefinedConflictStrategy_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new ImportOptions(onConflict: (ConflictStrategy)99));

    [Theory]
    [InlineData(ConflictStrategy.Error)]
    [InlineData(ConflictStrategy.Skip)]
    public void DefinedConflictStrategy_IsAccepted(ConflictStrategy strategy)
        => Assert.Equal(strategy, new ImportOptions(onConflict: strategy).OnConflict);
}
