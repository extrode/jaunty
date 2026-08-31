using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Internals.Entity;
using Jaunty.Tests.Helpers.Dialects;

using Xunit;

namespace Jaunty.Tests.Integration.Write;

/// <summary>
/// AUD-R35-099, the round-35 batch-03c coverage low. AUD-R35-011 fixed <c>[EnumStorage]</c> being
/// dropped by the multi-row <c>BulkInsert</c> bind site, and closed with the honest note that the
/// path itself was unreachable from any suite: it needs source-generated metadata, where
/// <c>ColumnMetadata.Property</c> is null, <em>and</em> a non-SQLite dialect, because <c>BulkInsert</c>
/// excludes SQLite from the multi-row path. The only project with the generator is
/// <c>Jaunty.SourceGenerator.Tests</c>, which is SQLite-only. So the fix was pinned by
/// <c>EnumStorageOverrideCallSiteTests</c> at the mechanism level and never executed end to end.
/// <para>
/// The generator is not the only way to get the generated <em>shape</em>.
/// <see cref="JauntyConfig.ReflectionTableMetadataResolver"/> is a supported public hook that
/// <c>CrudSqlCache</c> and <c>WriteParameterCache</c> both consult, so handing it columns built
/// through the getter/setter constructor - the one the source generator uses, which never populates
/// <c>Property</c> - reproduces exactly the condition the defect needed, against a real server.
/// </para>
/// <para>
/// Two rows, not one: one row takes the loop path, which was never affected.
/// </para>
/// </summary>
[Collection("Write Operations")]
public class BulkInsertMultiRowEnumStorageTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public BulkInsertMultiRowEnumStorageTests(DialectFixture fixture) => _fixture = fixture;

    public enum Ticket
    {
        Open = 0,
        Closed = 1,
    }

    public sealed class EnumBulkRow
    {
        public long Id { get; set; }
        public Ticket Name { get; set; }
        public int Value { get; set; }
    }

    /// <summary>
    /// The generated shape: built through the getter/setter constructor, so <c>Property</c> is null
    /// and the only thing carrying the storage choice is <c>enumStorageOverride</c>.
    /// </summary>
    private static EntityMetadata GeneratedMetadata(EnumStorage? storage) => new(
        tableName: "bulk_test",
        schemaName: null,
        columns:
        [
            new ColumnMetadata(
                propertyName: nameof(EnumBulkRow.Id),
                propertyType: typeof(long),
                columnName: "id",
                isPrimaryKey: true,
                isIdentity: true,
                isComputed: false,
                getter: static o => ((EnumBulkRow)o).Id,
                setter: static (o, v) => ((EnumBulkRow)o).Id = (long)v!),
            new ColumnMetadata(
                propertyName: nameof(EnumBulkRow.Name),
                propertyType: typeof(Ticket),
                columnName: "name",
                isPrimaryKey: false,
                isIdentity: false,
                isComputed: false,
                getter: static o => ((EnumBulkRow)o).Name,
                setter: static (o, v) => ((EnumBulkRow)o).Name = (Ticket)v!,
                enumStorageOverride: storage),
            new ColumnMetadata(
                propertyName: nameof(EnumBulkRow.Value),
                propertyType: typeof(int),
                columnName: "value",
                isPrimaryKey: false,
                isIdentity: false,
                isComputed: false,
                getter: static o => ((EnumBulkRow)o).Value,
                setter: static (o, v) => ((EnumBulkRow)o).Value = (int)v!),
        ]);

    private static readonly List<EnumBulkRow> TwoRows =
    [
        new() { Name = Ticket.Closed, Value = 1 },
        new() { Name = Ticket.Open, Value = 2 },
    ];

    private static List<string> Run(WriteDialectContext ctx, EnumStorage? storage)
    {
        Func<Type, object>? previous = JauntyConfig.ReflectionTableMetadataResolver;
        JauntyConfig.ReflectionTableMetadataResolver =
            t => t == typeof(EnumBulkRow) ? GeneratedMetadata(storage) : null!;

        try
        {
            int inserted = ctx.Connection.BulkInsert(TwoRows);
            Assert.Equal(2, inserted);

            return [.. ctx.Connection.QueryPartialList("SELECT name FROM bulk_test ORDER BY value")
                .Select(r => r["name"]?.ToString() ?? string.Empty)];
        }
        finally
        {
            JauntyConfig.ReflectionTableMetadataResolver = previous;
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void MultiRowBulkInsert_WithGeneratedShapeMetadata_WritesTheEnumName(DialectInfo dialect)
    {
        using WriteDialectContext ctx = _fixture.GetWriteContext(dialect);

        List<string> names = Run(ctx, EnumStorage.String);

        Assert.Equal(["Closed", "Open"], names);
    }

    /// <summary>
    /// The other half of the contract, and the control that says the test is reading a real write:
    /// with no override the same path falls back to <see cref="JauntyConfig.DefaultEnumStorage"/>,
    /// which stores the number. If the assertion above passed because the column simply echoed
    /// whatever it was given, this one would show "Closed" too.
    /// </summary>
    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void MultiRowBulkInsert_WithNoOverride_WritesTheEnumNumber(DialectInfo dialect)
    {
        using WriteDialectContext ctx = _fixture.GetWriteContext(dialect);

        List<string> names = Run(ctx, storage: null);

        Assert.Equal(["1", "0"], names);
    }
}
