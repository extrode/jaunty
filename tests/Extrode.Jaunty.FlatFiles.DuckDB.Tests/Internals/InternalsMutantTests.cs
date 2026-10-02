using System.Reflection;

using DuckDB.NET.Data;

using Extrode.Jaunty.FlatFiles.DuckDB.Internals;
using Extrode.Jaunty.FlatFiles.DuckDB.Internals.Import;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Internals;

public class InternalsMutantTests
{
    public class NullableDates
    {
        public int Id { get; set; }
        public DateTime? Plain { get; set; }
        public DateTimeOffset? Offset { get; set; }
        public int? Count { get; set; }
    }

    public class TextColumn
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Note { get; set; }
    }

    public class ClashingColumns
    {
        public int Code { get; set; }

        [Extrode.Jaunty.Attributes.Column("code")]
        public int Other { get; set; }
    }

    [Fact]
    public void ANullableDateProperty_IsTreatedAsADate()
    {
        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(typeof(NullableDates));

        Assert.True(mappings["Plain"].IsDateTime);
        Assert.True(mappings["Offset"].IsDateTime);
        Assert.False(mappings["Count"].IsDateTime);
        Assert.False(mappings["Id"].IsDateTime);
    }

    [Fact]
    public void TheNullabilityContext_IsCreatedOnceAndReusedOnTheThread()
    {
        FieldInfo field = typeof(TargetDdlGenerator).GetField("t_nullabilityContext", BindingFlags.NonPublic | BindingFlags.Static)!;

        TargetDdlGenerator.GetColumnDefinitions(typeof(TextColumn));
        object? first = field.GetValue(null);
        TargetDdlGenerator.GetColumnDefinitions(typeof(TextColumn));

        Assert.NotNull(first);
        Assert.Same(first, field.GetValue(null));
    }

    [Fact]
    public void TheNullableAnnotationDecidesNullabilityOfAReferenceColumn()
    {
        var columns = TargetDdlGenerator.GetColumnDefinitions(typeof(TextColumn));

        Assert.False(columns.Single(c => c.Name == "Name").IsNullable);
        Assert.True(columns.Single(c => c.Name == "Note").IsNullable);
    }

    [Fact]
    public void TwoPropertiesOnOneColumn_AreRejectedWithTheFullAdvice()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => TargetDdlGenerator.GetColumnDefinitions(typeof(ClashingColumns)));

        Assert.Equal(
            "Entity 'ClashingColumns' maps more than one property to column 'code': 'Code' and 'Other'. " +
            "Column names are compared case-insensitively because the target databases compare identifiers that way. " +
            "Give one of them a distinct [Column(\"...\")] name, or mark it [Ignore].",
            ex.Message);
    }

    [Fact]
    public void ADateTimeIsConvertedToADateTimeOffset()
    {
        var value = new DateTime(2024, 3, 9, 12, 30, 15, DateTimeKind.Utc);

        bool ok = ReaderValueConverter.TryConvert(value, typeof(DateTimeOffset?), false, out object? converted, out string? reason);

        Assert.True(ok);
        Assert.Null(reason);
        Assert.Equal(new DateTimeOffset(value), converted);
    }

    [Fact]
    public async Task TheDuckDbDriverCompletesItsAsyncCallsSynchronously()
    {
        await using var connection = new DuckDBConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var beginning = connection.BeginTransactionAsync();
        Assert.True(beginning.IsCompleted);
        var transaction = await beginning;

        var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        Task<int> executing = command.ExecuteNonQueryAsync();
        Assert.True(executing.IsCompleted);
        await executing;

        Task committing = transaction.CommitAsync();
        Assert.True(committing.IsCompleted);
        await committing;

        Assert.True(transaction.DisposeAsync().IsCompleted);
        Assert.True(command.DisposeAsync().IsCompleted);
    }
}
